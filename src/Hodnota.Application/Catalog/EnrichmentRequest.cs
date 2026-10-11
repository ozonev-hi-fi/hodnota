namespace Hodnota.Application.Catalog;

// TrackTitles: the catalog's own track titles for a Release request (null/empty for a Track
// request, or a Release whose tracks aren't known yet) — YouTube's channel disambiguation needs
// them (ADR 0018 D2/E10); other providers ignore it.
public sealed record EnrichmentRequest(
    StreamingResultType Type,
    string Name,
    string ArtistName,
    string? Isrc,
    string? Upc,
    IReadOnlyList<ProviderLinkCandidate> ExistingLinks,
    IReadOnlySet<string>? OnlyProviders = null,
    IReadOnlyList<string>? TrackTitles = null);
