using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Quartz;

namespace Hodnota.Infrastructure.Catalog;

// Two overlapping runs would both select, and both save, the same due candidates: a candidate's
// CheckedAtUtc only moves once its save completes.
[DisallowConcurrentExecution]
public sealed class ScheduledRefreshJob(
    IServiceScopeFactory scopeFactory,
    IRefreshSettingsCache settingsCache,
    ILogger<ScheduledRefreshJob> logger) : IJob
{
    private const int BatchSize = 50;

    private static readonly IReadOnlySet<string> InScopeProviderCodes = new HashSet<string>
    {
        ProviderCodes.Discogs,
        ProviderCodes.Tidal,
        ProviderCodes.Deezer,
        ProviderCodes.AppleMusic,
        ProviderCodes.Spotify,
        ProviderCodes.Qobuz,
    };

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var intervals = await settingsCache.GetIntervalsAsync(cancellationToken);

        IReadOnlyDictionary<string, IStreamingProvider> providersByPlatformCode;
        IReadOnlyList<DueRefreshCandidate> due;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            // Only providers registered right now: a dormant one (Spotify, Qobuz without credentials)
            // could never move its rows' CheckedAtUtc, so they would stay oldest and fill every batch.
            providersByPlatformCode = scope.ServiceProvider.GetServices<IStreamingProvider>()
                .Where(provider => InScopeProviderCodes.Contains(provider.ProviderCode))
                .SelectMany(provider => provider.LinkPlatformCodes.Select(code => (code, provider)))
                .ToDictionary(pair => pair.code, pair => pair.provider);
            if (providersByPlatformCode.Count == 0)
            {
                return;
            }

            var repository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
            due = await repository.GetDueProviderChecksAsync(
                [.. providersByPlatformCode.Keys],
                TimeSpan.FromDays(intervals.NotFoundRecheckIntervalDays),
                TimeSpan.FromDays(intervals.FoundRecheckIntervalDays),
                BatchSize,
                cancellationToken);
        }

        foreach (var page in due.GroupBy(candidate => candidate.SharePageId))
        {
            var providers = page.Select(candidate => providersByPlatformCode[candidate.PlatformCode]).Distinct().ToList();
            await RefreshSharePageAsync(page.Key, providers, cancellationToken);
        }
    }

    // A scope per page, like EnrichmentWorker: a failed save can leave the DbContext unusable, and
    // that must not spill into the rest of the batch.
    private async Task RefreshSharePageAsync(Guid sharePageId, IReadOnlyList<IStreamingProvider> providers, CancellationToken cancellationToken)
    {
        var settled = new HashSet<string>();
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
            var enrichment = scope.ServiceProvider.GetRequiredService<CatalogEnrichmentService>();

            var request = await repository.GetEnrichmentRequestAsync(sharePageId, cancellationToken);
            if (request is null)
            {
                return;
            }

            var onlyProviders = providers.Select(provider => provider.ProviderCode).ToHashSet();
            await foreach (var result in enrichment.EnrichAsync(request with { OnlyProviders = onlyProviders }, cancellationToken))
            {
                try
                {
                    await repository.SaveEnrichmentAsync(sharePageId, result, cancellationToken);
                    settled.Add(result.ProviderCode);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Saving the {ProviderCode} scheduled refresh of share page {SharePageId} failed.", result.ProviderCode, sharePageId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled refresh of share page {SharePageId} failed.", sharePageId);
        }

        await RecordUnsettledAsFailedAsync(sharePageId, providers.Where(provider => !settled.Contains(provider.ProviderCode)), cancellationToken);
    }

    // Moves CheckedAtUtc forward for every candidate this run picked but could not settle. Without
    // that, the candidate stays the oldest due row and is picked again by every later sweep.
    private async Task RecordUnsettledAsFailedAsync(Guid sharePageId, IEnumerable<IStreamingProvider> unsettled, CancellationToken cancellationToken)
    {
        foreach (var provider in unsettled)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
                var failed = new ProviderEnrichment(provider.ProviderCode, provider.LinkPlatformCodes, LookupOutcome.Failed, [], Confirmed: false);
                await repository.SaveEnrichmentAsync(sharePageId, failed, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Recording the failed {ProviderCode} scheduled refresh of share page {SharePageId} also failed.", provider.ProviderCode, sharePageId);
            }
        }
    }
}
