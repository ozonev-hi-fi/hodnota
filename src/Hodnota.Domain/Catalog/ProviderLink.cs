using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

// Exactly one of ArtistId/ReleaseId/TrackId is set — see ProviderLinkConfiguration.
public sealed class ProviderLink : IHasTimestamps
{
    public Guid Id { get; set; }

    public Guid? ArtistId { get; set; }

    public Guid? ReleaseId { get; set; }

    public Guid? TrackId { get; set; }

    public Guid PlatformId { get; set; }

    [Description("The platform's own identifier for the linked entity (e.g. a Spotify track ID).")]
    public required string ExternalId { get; set; }

    [Description("Deep link to the entity on the platform.")]
    public required Uri ExternalUrl { get; set; }

    [Description("How confident the matching process is that this link points to the correct catalog entity (0-1); null until matching logic sets it.")]
    public double? Confidence { get; set; }

    [Description("When this link was last confirmed to still resolve; null until verified.")]
    public DateTimeOffset? LastVerifiedUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Artist? Artist { get; set; }

    public Release? Release { get; set; }

    public Track? Track { get; set; }

    public Platform Platform { get; set; } = null!;
}
