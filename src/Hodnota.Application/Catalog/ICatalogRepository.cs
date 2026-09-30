using Hodnota.Domain.Catalog;

namespace Hodnota.Application.Catalog;

public interface ICatalogRepository
{
    // Finds the catalog item the result stands for (by a stored provider link, else by ISRC/UPC) and
    // returns its share page; creates the item and the page only when neither exists yet.
    Task<SharePageResult> ResolveSharePageAsync(StreamingSearchResult result, CancellationToken cancellationToken);

    Task<SharePageResult?> GetSharePageAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformCheckResult>> GetPlatformChecksAsync(Guid sharePageId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, PlatformType>> GetPlatformTypesAsync(IReadOnlyCollection<string> platformCodes, CancellationToken cancellationToken);

    Task<EnrichmentRequest?> GetEnrichmentRequestAsync(Guid sharePageId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformCheckResult>> SaveEnrichmentAsync(Guid sharePageId, ProviderEnrichment enrichment, CancellationToken cancellationToken);
}
