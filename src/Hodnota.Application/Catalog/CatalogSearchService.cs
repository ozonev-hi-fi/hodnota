using Microsoft.Extensions.Logging;

namespace Hodnota.Application.Catalog;

public sealed class CatalogSearchService(
    IEnumerable<IStreamingProvider> providers,
    ISearchCandidateCache cache,
    ILogger<CatalogSearchService> logger)
{
    private const int MaxMergedResults = 20;

    public async Task<IReadOnlyList<CatalogSearchCandidate>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var ordered = ProviderTrustOrder.Sort(providers);
        var attempts = await Task.WhenAll(ordered.Select(provider => SearchProviderAsync(provider, query, cancellationToken)));

        var failures = attempts.Where(attempt => attempt.Failure is not null).Select(attempt => attempt.Failure!).ToList();
        if (attempts.Length > 0 && failures.Count == attempts.Length)
        {
            throw new StreamingProviderException("Every streaming provider failed.", new AggregateException(failures));
        }

        var merged = SearchResultMerger.Merge(attempts.Select(attempt => attempt.Results));

        return [.. merged.Take(MaxMergedResults).Select(result => new CatalogSearchCandidate(cache.Store(result), result))];
    }

    private async Task<ProviderAttempt> SearchProviderAsync(IStreamingProvider provider, string query, CancellationToken cancellationToken)
    {
        try
        {
            return new ProviderAttempt(await provider.SearchAsync(query, cancellationToken), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Streaming provider {ProviderCode} failed; skipping it for this search.", provider.ProviderCode);
            return new ProviderAttempt([], ex);
        }
    }

    private readonly record struct ProviderAttempt(IReadOnlyList<StreamingSearchResult> Results, Exception? Failure);
}
