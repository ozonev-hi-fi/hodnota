using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

public sealed class Track : IHasTimestamps
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public int? DurationMs { get; set; }

    public bool IsExplicit { get; set; }

    [Description("International Standard Recording Code — a natural key for matching the same recording across providers.")]
    public string? Isrc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<ReleaseTrack> Releases { get; set; } = [];

    public ICollection<ArtistCredit> Credits { get; set; } = [];

    public ICollection<ProviderLink> ProviderLinks { get; set; } = [];

    public ICollection<EntityGenre> Genres { get; set; } = [];
}
