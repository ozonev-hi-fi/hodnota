namespace Hodnota.Domain.Catalog;

public sealed class Artist : IHasTimestamps
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public ArtistType Type { get; set; } = ArtistType.Unknown;

    public Uri? ImageUrl { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<ArtistCredit> Credits { get; set; } = [];

    public ICollection<ProviderLink> ProviderLinks { get; set; } = [];

    public ICollection<EntityGenre> Genres { get; set; } = [];
}
