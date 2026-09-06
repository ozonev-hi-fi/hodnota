namespace Hodnota.Application.Catalog;

public sealed record SharePageLinkResult(string PlatformCode, Uri Url);

public sealed record SharePageResult(
    Guid Id,
    StreamingResultType Type,
    string Name,
    string ArtistName,
    IReadOnlyList<SharePageLinkResult> Links);
