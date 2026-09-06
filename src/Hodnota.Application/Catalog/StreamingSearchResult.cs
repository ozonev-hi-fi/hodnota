namespace Hodnota.Application.Catalog;

public sealed record StreamingSearchResult(
    StreamingResultType Type,
    string Name,
    string ArtistName,
    Uri? ImageUrl,
    IReadOnlyList<ProviderLinkCandidate> Links);
