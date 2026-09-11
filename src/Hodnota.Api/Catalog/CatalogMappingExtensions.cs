using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

using DomainPlatformType = Hodnota.Domain.Catalog.PlatformType;

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
        [.. result.Links.Select(link => new SharePageLinkResponse(link.PlatformCode, link.Url, link.PlatformType.ToContractType()))]);

    private static CandidateType ToCandidateType(this StreamingResultType type) =>
        type == StreamingResultType.Track ? CandidateType.Song : CandidateType.Album;

    private static PlatformType ToContractType(this DomainPlatformType type) => type switch
    {
        DomainPlatformType.StreamingService => PlatformType.StreamingService,
        DomainPlatformType.DigitalStore => PlatformType.DigitalStore,
        DomainPlatformType.PhysicalStore => PlatformType.PhysicalStore,
        DomainPlatformType.Aggregator => PlatformType.Aggregator,
        DomainPlatformType.Database => PlatformType.Database,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
