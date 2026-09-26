using System.Text.Json.Serialization;

namespace Hodnota.Contracts.Catalog;

[JsonConverter(typeof(StrictStringEnumConverter<CandidateType>))]
public enum CandidateType
{
    Song,
    Album,
}
