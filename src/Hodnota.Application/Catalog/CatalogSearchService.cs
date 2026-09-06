namespace Hodnota.Application.Catalog;

public sealed class CatalogSearchService(IEnumerable<IStreamingProvider> providers, ISearchCandidateCache cache)
{
    public async Task<IReadOnlyList<CatalogSearchCandidate>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var resultsByProvider = await Task.WhenAll(providers.Select(provider => provider.SearchAsync(query, cancellationToken)));

        return [.. resultsByProvider
            .SelectMany(results => results)
            .Select(result => new CatalogSearchCandidate(cache.Store(result), result))];
    }
}
