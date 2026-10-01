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

public sealed record PlatformRowResponse(string Platform, PlatformType Type, PlatformRowState State, Uri? Url);

public sealed record SharePageResponse(
    Guid Id,
    CandidateType Type,
    string Name,
    string Artist,
    IReadOnlyList<PlatformRowResponse> Platforms,
    bool IsComplete);
