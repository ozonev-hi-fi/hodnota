using System.Text.Json.Serialization;

namespace Hodnota.Contracts.Catalog;

[JsonConverter(typeof(JsonStringEnumConverter<CandidateType>))]
public enum CandidateType
{
    Song,
    Album,
}
