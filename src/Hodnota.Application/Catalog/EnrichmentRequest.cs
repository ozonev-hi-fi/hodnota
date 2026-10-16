using System.ComponentModel;

namespace Hodnota.Application.Catalog;

public sealed record EnrichmentRequest(
    StreamingResultType Type,
    string Name,
    string ArtistName,
    string? Isrc,
    string? Upc,
    IReadOnlyList<ProviderLinkCandidate> ExistingLinks,
    IReadOnlySet<string>? OnlyProviders = null,
    [property: Description("The catalog's own track titles of a Release request; null for a Track request.")] IReadOnlyList<string>? TrackTitles = null);
