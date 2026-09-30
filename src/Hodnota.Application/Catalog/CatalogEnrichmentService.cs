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

    private async Task<ProviderEnrichment> CheckAsync(IStreamingProvider provider, EnrichmentRequest request, StreamingLookupKey? key, CancellationToken cancellationToken)
    {
        var existing = request.ExistingLinks.Where(link => provider.LinkPlatformCodes.Contains(link.PlatformCode)).ToList();

        ProviderEnrichment Result(LookupOutcome outcome, IReadOnlyList<ProviderLinkCandidate> links) =>
            new(provider.ProviderCode, provider.LinkPlatformCodes, outcome, links);

        try
        {
            var canAskAgain = provider.SupportsLookup(request.Type);

            if (key is not null && canAskAgain)
            {
                var found = await provider.LookupAsync(key, cancellationToken);
                var match = found.FirstOrDefault(result => result.Type == request.Type && result.Links.Count > 0);
                if (match is not null)
                {
                    return Result(LookupOutcome.ExactMatch, match.Links);
                }
            }
            else if (existing.Count == 0 && key is null && canAskAgain)
            {
                var wanted = SearchResultKey.Build(new StreamingSearchResult(request.Type, request.Name, request.ArtistName, null, []));
                var found = await provider.SearchAsync($"{request.ArtistName} {request.Name}", request.Type, cancellationToken);
                var match = found.FirstOrDefault(result => result.Type == request.Type && result.Links.Count > 0 && SearchResultKey.Build(result) == wanted);
                if (match is not null)
                {
                    return Result(LookupOutcome.NameMatch, match.Links);
                }
            }

            return existing.Count > 0 ? Result(LookupOutcome.NameMatch, existing) : Result(LookupOutcome.NotFound, []);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Streaming provider {ProviderCode} failed; recording the check as failed.", provider.ProviderCode);
            return Result(LookupOutcome.Failed, []);
        }
    }
}
