using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

public sealed class Release : IHasTimestamps
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public required ReleaseType Type { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    [Description("Universal Product Code — a natural key for matching the same release across providers.")]
    public string? Upc { get; set; }

    public Uri? CoverArtUrl { get; set; }

    public Guid? LabelId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public RecordLabel? Label { get; set; }

    public ICollection<ReleaseTrack> Tracks { get; set; } = [];

    public ICollection<ArtistCredit> Credits { get; set; } = [];

    public ICollection<ProviderLink> ProviderLinks { get; set; } = [];

    public ICollection<EntityGenre> Genres { get; set; } = [];
}
