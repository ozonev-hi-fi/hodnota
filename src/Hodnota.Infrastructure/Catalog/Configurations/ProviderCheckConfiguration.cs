using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class ProviderCheckConfiguration : IEntityTypeConfiguration<ProviderCheck>
{
    public void Configure(EntityTypeBuilder<ProviderCheck> builder)
    {
        builder.Property(x => x.Outcome).HasConversion<string>();

        builder.HasOne(x => x.Artist)
            .WithMany()
            .HasForeignKey(x => x.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Release)
            .WithMany()
            .HasForeignKey(x => x.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Track)
            .WithMany()
            .HasForeignKey(x => x.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Platform)
            .WithMany()
            .HasForeignKey(x => x.PlatformId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ProviderCheck_ExactlyOneTarget",
            ExactlyOneTargetCheckConstraint.Sql("ArtistId", "ReleaseId", "TrackId")));

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
