using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

// Exactly one of ReleaseId/TrackId is set — see ArtistCreditConfiguration.
public sealed class ArtistCredit
{
    public Guid Id { get; set; }

    public Guid ArtistId { get; set; }

    public Guid? ReleaseId { get; set; }

    public Guid? TrackId { get; set; }

    public required CreditRole Role { get; set; }

    [Description("Display order among multiple credits sharing the same Role on the same Release/Track.")]
    public int CreditOrder { get; set; } = 1;

    public Artist Artist { get; set; } = null!;

    public Release? Release { get; set; }

    public Track? Track { get; set; }
}
