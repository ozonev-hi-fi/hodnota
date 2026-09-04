using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

public sealed class ReleaseTrack
{
    public Guid Id { get; set; }

    public Guid ReleaseId { get; set; }

    public Guid TrackId { get; set; }

    public int DiscNumber { get; set; } = 1;

    public int TrackNumber { get; set; }

    [Description("Alternate title as printed on this specific release (e.g. a live/remix edit), overriding Track.Title for display.")]
    public string? TitleOverride { get; set; }

    public Release Release { get; set; } = null!;

    public Track Track { get; set; } = null!;
}
