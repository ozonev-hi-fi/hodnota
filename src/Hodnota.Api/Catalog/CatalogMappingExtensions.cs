using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

namespace Hodnota.Api.Catalog;

public static class CatalogMappingExtensions
{
    public static SearchCandidateResponse ToResponse(this CatalogSearchCandidate candidate) => new(
        candidate.CandidateId,
        candidate.Result.Type.ToCandidateType(),
        candidate.Result.Name,
        candidate.Result.ArtistName,
        candidate.Result.ImageUrl);

    public static SharePageResponse ToResponse(this SharePageResult result) => new(
        result.Id,
        result.Type.ToCandidateType(),
        result.Name,
        result.ArtistName,
        [.. result.Links.Select(link => new SharePageLinkResponse(link.PlatformCode, link.Url))]);

    private static CandidateType ToCandidateType(this StreamingResultType type) =>
        type == StreamingResultType.Track ? CandidateType.Song : CandidateType.Album;
}
