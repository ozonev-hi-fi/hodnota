using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class ProviderLinkConfiguration : IEntityTypeConfiguration<ProviderLink>
{
    public void Configure(EntityTypeBuilder<ProviderLink> builder)
    {
        builder.Property(x => x.ExternalId).IsRequired();
        builder.Property(x => x.ExternalUrl).IsRequired();

        builder.HasOne(x => x.Artist)
            .WithMany(x => x.ProviderLinks)
            .HasForeignKey(x => x.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Release)
            .WithMany(x => x.ProviderLinks)
            .HasForeignKey(x => x.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Track)
            .WithMany(x => x.ProviderLinks)
            .HasForeignKey(x => x.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Platform)
            .WithMany(x => x.ProviderLinks)
            .HasForeignKey(x => x.PlatformId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ProviderLink_ExactlyOneTarget",
            """
            (CASE WHEN "ArtistId" IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN "ReleaseId" IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN "TrackId" IS NOT NULL THEN 1 ELSE 0 END) = 1
            """));

        builder.HasIndex(x => new { x.PlatformId, x.ArtistId })
            .IsUnique()
            .HasFilter("\"ArtistId\" IS NOT NULL");

        builder.HasIndex(x => new { x.PlatformId, x.ReleaseId })
            .IsUnique()
            .HasFilter("\"ReleaseId\" IS NOT NULL");

        builder.HasIndex(x => new { x.PlatformId, x.TrackId })
            .IsUnique()
            .HasFilter("\"TrackId\" IS NOT NULL");
    }
}
