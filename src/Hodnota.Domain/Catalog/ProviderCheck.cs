using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

// Exactly one of ArtistId/ReleaseId/TrackId is set — polymorphic.
public sealed class ProviderCheck : IHasTimestamps
{
    public Guid Id { get; set; }

    public Guid? ArtistId { get; set; }

    public Guid? ReleaseId { get; set; }

    public Guid? TrackId { get; set; }

    public Guid PlatformId { get; set; }

    [Description("What the last check of this platform found for the entity.")]
    public LookupOutcome Outcome { get; set; }

    [Description("When the platform was last asked about the entity.")]
    public DateTimeOffset CheckedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Artist? Artist { get; set; }

    public Release? Release { get; set; }

    public Track? Track { get; set; }

    public Platform Platform { get; set; } = null!;
}
