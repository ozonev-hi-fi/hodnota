using System.Text.Json.Serialization;

namespace Hodnota.Contracts.Catalog;

[JsonConverter(typeof(StrictStringEnumConverter<PlatformType>))]
public enum PlatformType
{
    StreamingService,
    DigitalStore,
    PhysicalStore,
    Aggregator,
    Database,
}

[JsonConverter(typeof(StrictStringEnumConverter<PlatformRowState>))]
public enum PlatformRowState
{
    Checking,
    Found,
    OtherVersion,
    NotFound,
    Failed,
}

// ResultType is set only when it differs from the page's own type — YouTube's verified-track
// fallback for an unverified album (ADR 0018 D2) is the only case today.
public sealed record PlatformRowResponse(string Platform, PlatformType Type, PlatformRowState State, Uri? Url, CandidateType? ResultType = null);

public sealed record SharePageResponse(
    Guid Id,
    CandidateType Type,
    string Name,
    string Artist,
    IReadOnlyList<PlatformRowResponse> Platforms,
    bool IsComplete);
