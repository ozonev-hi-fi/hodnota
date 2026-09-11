using System.Text.Json.Serialization;

namespace Hodnota.Contracts.Catalog;

[JsonConverter(typeof(JsonStringEnumConverter<PlatformType>))]
public enum PlatformType
{
    StreamingService,
    DigitalStore,
    PhysicalStore,
    Aggregator,
    Database,
}

public sealed record SharePageLinkResponse(string Platform, Uri Url, PlatformType Type);

public sealed record SharePageResponse(
    Guid Id,
    CandidateType Type,
    string Name,
    string Artist,
    IReadOnlyList<SharePageLinkResponse> Links);
