using System.ComponentModel;

using Hodnota.Domain.Catalog;

namespace Hodnota.Application.Catalog;

public sealed record StreamingSearchResult(
    StreamingResultType Type,
    string Name,
    string ArtistName,
    Uri? ImageUrl,
    IReadOnlyList<ProviderLinkCandidate> Links,
    ReleaseType? ReleaseType = null,
    [property: Description("International Standard Recording Code — a natural key for matching the same recording across providers.")] string? Isrc = null,
    [property: Description("Universal Product Code — a natural key for matching the same release across providers.")] string? Upc = null);
