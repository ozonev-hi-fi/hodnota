namespace Hodnota.Domain.Catalog;

// Exactly one of ArtistId/ReleaseId/TrackId is set — see SharePageConfiguration.
public sealed class SharePage : IHasTimestamps
{
    public Guid Id { get; set; }

    public Guid? ArtistId { get; set; }

    public Guid? ReleaseId { get; set; }

    public Guid? TrackId { get; set; }

    public Guid? UserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Artist? Artist { get; set; }

    public Release? Release { get; set; }

    public Track? Track { get; set; }

    public ICollection<SharePageLink> Links { get; set; } = [];
}
