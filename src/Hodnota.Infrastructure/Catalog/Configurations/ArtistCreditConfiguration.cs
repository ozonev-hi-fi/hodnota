using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

// Reference implementation for the polymorphic nullable-FK + CHECK constraint + partial unique
// index pattern also used by ProviderLinkConfiguration and EntityGenreConfiguration.
public sealed class ArtistCreditConfiguration : IEntityTypeConfiguration<ArtistCredit>
{
    public void Configure(EntityTypeBuilder<ArtistCredit> builder)
    {
        builder.Property(x => x.Role).HasConversion<string>();
        builder.Property(x => x.CreditOrder).HasDefaultValue(1);

        builder.HasOne(x => x.Artist)
            .WithMany(x => x.Credits)
            .HasForeignKey(x => x.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Release)
            .WithMany(x => x.Credits)
            .HasForeignKey(x => x.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Track)
            .WithMany(x => x.Credits)
            .HasForeignKey(x => x.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ArtistCredit_ExactlyOneTarget",
            """
            (CASE WHEN "ReleaseId" IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN "TrackId" IS NOT NULL THEN 1 ELSE 0 END) = 1
            """));

        builder.HasIndex(x => x.ReleaseId)
            .IsUnique()
            .HasFilter("\"Role\" = 'MainArtist' AND \"ReleaseId\" IS NOT NULL");

        builder.HasIndex(x => x.TrackId)
            .IsUnique()
            .HasFilter("\"Role\" = 'MainArtist' AND \"TrackId\" IS NOT NULL");
    }
}
