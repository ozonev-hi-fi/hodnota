using System.ComponentModel.DataAnnotations;

namespace Hodnota.Contracts.Catalog;

public sealed record SearchRequest([Required] string Search);
