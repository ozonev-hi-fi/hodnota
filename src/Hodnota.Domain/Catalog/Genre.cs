using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

public sealed class Genre : IHasTimestamps
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    [Description("URL-safe identifier derived from Name, e.g. \"synth-pop\".")]
    public required string Slug { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<EntityGenre> Entities { get; set; } = [];
}
