using Hodnota.Domain.Catalog;

namespace Hodnota.Application.Catalog;

public sealed record SharePageLinkResult(string PlatformCode, Uri Url, PlatformType PlatformType);

public sealed record SharePageResult(
    Guid Id,
    StreamingResultType Type,
    string Name,
    string ArtistName,
    IReadOnlyList<SharePageLinkResult> Links);
