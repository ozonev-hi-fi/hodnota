namespace Hodnota.Contracts.Catalog;

public sealed record SharePageLinkResponse(string Platform, Uri Url);

public sealed record SharePageResponse(
    Guid Id,
    CandidateType Type,
    string Name,
    string Artist,
    IReadOnlyList<SharePageLinkResponse> Links);
