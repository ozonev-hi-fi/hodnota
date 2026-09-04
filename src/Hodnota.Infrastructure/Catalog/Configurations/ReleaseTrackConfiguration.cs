using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class ReleaseTrackConfiguration : IEntityTypeConfiguration<ReleaseTrack>
{
    public void Configure(EntityTypeBuilder<ReleaseTrack> builder)
    {
        builder.Property(x => x.DiscNumber).HasDefaultValue(1);

        builder.HasOne(x => x.Release)
            .WithMany(x => x.Tracks)
            .HasForeignKey(x => x.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Track)
            .WithMany(x => x.Releases)
            .HasForeignKey(x => x.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ReleaseId, x.DiscNumber, x.TrackNumber }).IsUnique();
    }
}
