using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Catalog;

public sealed class EnrichmentWorker(
    InProcessEnrichmentQueue queue,
    IShareEventHub events,
    IServiceScopeFactory scopeFactory,
    ILogger<EnrichmentWorker> logger) : BackgroundService
{
    private const int MaxConcurrentJobs = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var slots = new SemaphoreSlim(MaxConcurrentJobs);
        var running = new List<Task>();

        try
        {
            await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await slots.WaitAsync(stoppingToken);
                running.RemoveAll(task => task.IsCompleted);
                running.Add(RunJobAsync(job, slots, stoppingToken));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }

        await Task.WhenAll(running);
    }

    private async Task RunJobAsync(EnrichmentJob job, SemaphoreSlim slots, CancellationToken cancellationToken)
    {
        // Every code the job started with must end up either here (really checked, or recorded as
        // failed) or left out on purpose (the page no longer exists) — a viewer must never be left
        // watching a spinner for a platform the worker has already given up on.
        var settled = new HashSet<string>();
        IReadOnlyDictionary<string, IReadOnlyList<string>>? platformCodesByProvider = null;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
            var enrichment = scope.ServiceProvider.GetRequiredService<CatalogEnrichmentService>();
            platformCodesByProvider = scope.ServiceProvider.GetServices<IStreamingProvider>()
                .ToDictionary(provider => provider.ProviderCode, provider => provider.LinkPlatformCodes);

            var request = await repository.GetEnrichmentRequestAsync(job.SharePageId, cancellationToken);
            if (request is null)
            {
                settled.UnionWith(job.ProviderCodes);
                return;
            }

            await foreach (var result in enrichment.EnrichAsync(request with { OnlyProviders = job.ProviderCodes.ToHashSet() }, cancellationToken))
            {
                await SaveAndPublishAsync(repository, job.SharePageId, result, settled, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down; the unfinished checks are queued again the next time the page is used.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Enrichment of share page {SharePageId} failed.", job.SharePageId);
        }
        finally
        {
            foreach (var code in job.ProviderCodes)
            {
                queue.Release(job.SharePageId, code);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                PublishUnsettledAsFailed(job, settled, platformCodesByProvider);
            }

            slots.Release();
        }
    }

    // One provider's failed save must not drop the others' results. A failure still gets one more
    // save attempt, as "failed" — so the platform is recorded as checked (not left to look
    // never-checked, which would queue it again on every future click) and the viewer is told now
    // rather than after the watch endpoint's 60 s timeout.
    private async Task SaveAndPublishAsync(ICatalogRepository repository, Guid sharePageId, ProviderEnrichment result, HashSet<string> settled, CancellationToken cancellationToken)
    {
        try
        {
            var saved = await repository.SaveEnrichmentAsync(sharePageId, result, cancellationToken);

            // Released before publishing: a viewer that subscribes after the publish reads the
            // saved state, and must find this platform settled, not still being checked.
            queue.Release(sharePageId, result.ProviderCode);
            events.Publish(sharePageId, saved);
            settled.Add(result.ProviderCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Saving the {ProviderCode} check of share page {SharePageId} failed.", result.ProviderCode, sharePageId);
            await SaveFailedFallbackAsync(repository, sharePageId, result, settled, cancellationToken);
        }
        finally
        {
            queue.Release(sharePageId, result.ProviderCode);
        }
    }

    private async Task SaveFailedFallbackAsync(ICatalogRepository repository, Guid sharePageId, ProviderEnrichment result, HashSet<string> settled, CancellationToken cancellationToken)
    {
        try
        {
            var failed = result with { Outcome = LookupOutcome.Failed, Links = [], Confirmed = false };
            var saved = await repository.SaveEnrichmentAsync(sharePageId, failed, cancellationToken);
            events.Publish(sharePageId, saved);
            settled.Add(result.ProviderCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Both saves failed (e.g. the database is down). The platform stays unsettled; the
            // caller's finally block below still tells the viewer, from the in-memory result alone.
            logger.LogError(ex, "Recording the failed {ProviderCode} check of share page {SharePageId} also failed.", result.ProviderCode, sharePageId);
        }
    }

    // Covers every code the try block above never got to settle: a save that failed twice, or an
    // exception from GetEnrichmentRequestAsync/EnrichAsync itself. Published from memory, with
    // nothing saved, because the database is the part that is not trusted to work right now.
    private void PublishUnsettledAsFailed(EnrichmentJob job, HashSet<string> settled, IReadOnlyDictionary<string, IReadOnlyList<string>>? platformCodesByProvider)
    {
        if (platformCodesByProvider is null)
        {
            return;
        }

        foreach (var providerCode in job.ProviderCodes.Where(code => !settled.Contains(code)))
        {
            if (!platformCodesByProvider.TryGetValue(providerCode, out var platformCodes))
            {
                continue;
            }

            events.Publish(job.SharePageId, [.. platformCodes.Select(code => new PlatformCheckResult(code, LookupOutcome.Failed))]);
        }
    }
}
