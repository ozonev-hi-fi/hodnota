using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Hodnota.Contracts.Catalog;

public sealed record SearchRequest([Required] string Search, [property: JsonRequired] CandidateType Type);
