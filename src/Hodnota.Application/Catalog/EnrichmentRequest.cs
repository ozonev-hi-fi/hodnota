namespace Hodnota.Application.Catalog;

public sealed record EnrichmentRequest(
    StreamingResultType Type,
    string Name,
    string ArtistName,
    string? Isrc,
    string? Upc,
    IReadOnlyList<ProviderLinkCandidate> ExistingLinks,
    IReadOnlySet<string>? OnlyProviders = null);
