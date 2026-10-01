using System.Runtime.CompilerServices;

using Hodnota.Domain.Catalog;

using Microsoft.Extensions.Logging;

namespace Hodnota.Application.Catalog;

public sealed class CatalogEnrichmentService(IEnumerable<IStreamingProvider> providers, ILogger<CatalogEnrichmentService> logger)
{
    // Yields one result per provider as soon as that provider is done, so the caller can save it and
    // tell the open share page without waiting for the slowest provider.
    public async IAsyncEnumerable<ProviderEnrichment> EnrichAsync(EnrichmentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var key = BuildKey(request);

        // If the caller stops reading early (a failed save), the provider calls still running must
        // not outlive the caller's scope, so they are cancelled and awaited on the way out.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var checks = providers
            .Where(provider => provider.Supports(request.Type))
            .Where(provider => request.OnlyProviders is null || request.OnlyProviders.Contains(provider.ProviderCode))
            .Select(provider => CheckAsync(provider, request, key, stop.Token))
            .ToList();

        try
        {
            await foreach (var check in Task.WhenEach(checks).WithCancellation(cancellationToken))
            {
                yield return await check;
            }
        }
        finally
        {
            await stop.CancelAsync();
            foreach (var check in checks)
            {
                try
                {
                    await check;
                }
                catch (OperationCanceledException)
                {
                    // Cancelled on purpose just above.
                }
            }
        }
    }

    private static StreamingLookupKey? BuildKey(EnrichmentRequest request)
    {
        IReadOnlyList<string> codes = request.Type == StreamingResultType.Track
            ? CatalogKeys.NormalizeIsrc(request.Isrc) is { } isrc ? [isrc] : []
            : CatalogKeys.BarcodeVariants(request.Upc);

        return codes.Count == 0 ? null : new StreamingLookupKey(request.Type, codes);
    }

    private static async Task<IReadOnlyList<StreamingSearchResult>> FindByNameAsync(IStreamingProvider provider, EnrichmentRequest request, bool canSearch, CancellationToken cancellationToken)
    {
        IEnumerable<StreamingSearchResult> found;
        if (provider is IStreamingNameLookup byName)
        {
            found = await byName.FindByNameAsync(request.ArtistName, request.Name, request.Type, cancellationToken);
        }
        else if (canSearch)
        {
            var wanted = SearchResultKey.Build(new StreamingSearchResult(request.Type, request.Name, request.ArtistName, null, []));
            found = (await provider.SearchAsync($"{request.ArtistName} {request.Name}", request.Type, cancellationToken))
                .Where(result => SearchResultKey.Build(result) == wanted);
        }
        else
        {
            return [];
        }

        return [.. found.Where(result => result.Type == request.Type && result.Links.Count > 0)];
    }

    private async Task<ProviderEnrichment> CheckAsync(IStreamingProvider provider, EnrichmentRequest request, StreamingLookupKey? key, CancellationToken cancellationToken)
    {
        var existing = request.ExistingLinks.Where(link => provider.LinkPlatformCodes.Contains(link.PlatformCode)).ToList();

        ProviderEnrichment Result(LookupOutcome outcome, IReadOnlyList<ProviderLinkCandidate> links, bool confirmed) =>
            new(provider.ProviderCode, provider.LinkPlatformCodes, outcome, links, confirmed);

        try
        {
            var canAskAgain = provider.SupportsLookup(request.Type);

            if (key is not null && canAskAgain)
            {
                var found = await provider.LookupAsync(key, cancellationToken);
                var matches = found.Where(result => result.Type == request.Type && result.Links.Count > 0).ToList();
                if (matches.Count > 0)
                {
                    // Candidates in the provider's own preference order (e.g. a Discogs master, then
                    // its release) — the caller takes the first one not already taken by another entity.
                    return Result(LookupOutcome.ExactMatch, [.. matches.SelectMany(match => match.Links)], confirmed: true);
                }
            }
            else if (existing.Count == 0)
            {
                // Nothing to keep and no code to ask with: find the item by its artist and title.
                var matches = await FindByNameAsync(provider, request, canAskAgain, cancellationToken);
                if (matches.Count > 0)
                {
                    return Result(LookupOutcome.NameMatch, [.. matches.SelectMany(match => match.Links)], confirmed: true);
                }
            }

            return existing.Count > 0 ? Result(LookupOutcome.NameMatch, existing, confirmed: false) : Result(LookupOutcome.NotFound, [], confirmed: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Streaming provider {ProviderCode} failed; recording the check as failed.", provider.ProviderCode);
            return Result(LookupOutcome.Failed, [], confirmed: false);
        }
    }
}
