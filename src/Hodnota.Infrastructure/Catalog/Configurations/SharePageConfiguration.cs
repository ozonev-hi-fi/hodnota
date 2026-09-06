using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class SharePageConfiguration : IEntityTypeConfiguration<SharePage>
{
    public void Configure(EntityTypeBuilder<SharePage> builder)
    {
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

        // No CLR navigation on SharePage — Hodnota.Domain must not depend on Hodnota.Infrastructure.Identity.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_SharePage_ExactlyOneTarget",
            """
            (CASE WHEN "ArtistId" IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN "ReleaseId" IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN "TrackId" IS NOT NULL THEN 1 ELSE 0 END) = 1
            """));
    }
}
