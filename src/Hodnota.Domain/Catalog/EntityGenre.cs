namespace Hodnota.Domain.Catalog;

// Exactly one of ArtistId/ReleaseId/TrackId is set — see EntityGenreConfiguration.
public sealed class EntityGenre
{
    public Guid Id { get; set; }

    public Guid GenreId { get; set; }

    public Guid? ArtistId { get; set; }

    public Guid? ReleaseId { get; set; }

    public Guid? TrackId { get; set; }

    public Genre Genre { get; set; } = null!;

    public Artist? Artist { get; set; }

    public Release? Release { get; set; }

    public Track? Track { get; set; }
}
