using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;

using ApplicationRowState = Hodnota.Application.Catalog.PlatformRowState;
using ContractRowState = Hodnota.Contracts.Catalog.PlatformRowState;
using DomainPlatformType = Hodnota.Domain.Catalog.PlatformType;

namespace Hodnota.Api.Catalog;

public static class CatalogMappingExtensions
{
    public static SearchCandidateResponse ToResponse(this CatalogSearchCandidate candidate) => new(
        candidate.CandidateId,
        candidate.Result.Type.ToCandidateType(),
        candidate.Result.Name,
        candidate.Result.ArtistName,
        candidate.Result.ImageUrl,
        [.. candidate.Result.Links.Select(link => link.PlatformCode)]);

    public static SharePageResponse ToResponse(this SharePageView view) => new(
        view.Page.Id,
        view.Page.Type.ToCandidateType(),
        view.Page.Name,
        view.Page.ArtistName,
        [.. view.Rows.Select(row => row.ToResponse())],
        view.IsComplete);

    public static PlatformRowResponse ToResponse(this PlatformRow row) => new(
        row.PlatformCode,
        row.PlatformType.ToContractType(),
        row.State.ToContractState(),
        row.Url);

    private static ContractRowState ToContractState(this ApplicationRowState state) => state switch
    {
        ApplicationRowState.Checking => ContractRowState.Checking,
        ApplicationRowState.Found => ContractRowState.Found,
        ApplicationRowState.OtherVersion => ContractRowState.OtherVersion,
        ApplicationRowState.NotFound => ContractRowState.NotFound,
        ApplicationRowState.Failed => ContractRowState.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static StreamingResultType ToStreamingResultType(this CandidateType type) => type switch
    {
        CandidateType.Song => StreamingResultType.Track,
        CandidateType.Album => StreamingResultType.Release,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

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
