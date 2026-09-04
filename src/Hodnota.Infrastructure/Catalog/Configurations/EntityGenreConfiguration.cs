using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class EntityGenreConfiguration : IEntityTypeConfiguration<EntityGenre>
{
    public void Configure(EntityTypeBuilder<EntityGenre> builder)
    {
        builder.HasOne(x => x.Genre)
            .WithMany(x => x.Entities)
            .HasForeignKey(x => x.GenreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Artist)
            .WithMany(x => x.Genres)
            .HasForeignKey(x => x.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Release)
            .WithMany(x => x.Genres)
            .HasForeignKey(x => x.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Track)
            .WithMany(x => x.Genres)
            .HasForeignKey(x => x.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_EntityGenre_ExactlyOneTarget",
            """
            (CASE WHEN "ArtistId" IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN "ReleaseId" IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN "TrackId" IS NOT NULL THEN 1 ELSE 0 END) = 1
            """));

        // Three partial unique indexes, one per target type — see ProviderLinkConfiguration.
        builder.HasIndex(x => new { x.GenreId, x.ArtistId })
            .IsUnique()
            .HasFilter("\"ArtistId\" IS NOT NULL");

        builder.HasIndex(x => new { x.GenreId, x.ReleaseId })
            .IsUnique()
            .HasFilter("\"ReleaseId\" IS NOT NULL");

        builder.HasIndex(x => new { x.GenreId, x.TrackId })
            .IsUnique()
            .HasFilter("\"TrackId\" IS NOT NULL");
    }
}
