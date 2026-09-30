using Hodnota.Application.Catalog;

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
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
            var enrichment = scope.ServiceProvider.GetRequiredService<CatalogEnrichmentService>();

            var request = await repository.GetEnrichmentRequestAsync(job.SharePageId, cancellationToken);
            if (request is null)
            {
                return;
            }

            await foreach (var result in enrichment.EnrichAsync(request with { OnlyProviders = job.ProviderCodes.ToHashSet() }, cancellationToken))
            {
                // One provider's failed save must not drop the others' results.
                try
                {
                    var saved = await repository.SaveEnrichmentAsync(job.SharePageId, result, cancellationToken);

                    // Released before publishing: a viewer that subscribes after the publish reads the
                    // saved state, and must find this platform settled, not still being checked.
                    queue.Release(job.SharePageId, result.ProviderCode);
                    events.Publish(job.SharePageId, saved);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Saving the {ProviderCode} check of share page {SharePageId} failed.", result.ProviderCode, job.SharePageId);
                }
                finally
                {
                    queue.Release(job.SharePageId, result.ProviderCode);
                }
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

            slots.Release();
        }
    }
}
