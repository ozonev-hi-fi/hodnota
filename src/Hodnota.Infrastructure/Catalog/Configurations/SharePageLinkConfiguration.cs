using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class SharePageLinkConfiguration : IEntityTypeConfiguration<SharePageLink>
{
    public void Configure(EntityTypeBuilder<SharePageLink> builder)
    {
        builder.Property(x => x.IsVisible).HasDefaultValue(true);

        builder.HasOne(x => x.SharePage)
            .WithMany(x => x.Links)
            .HasForeignKey(x => x.SharePageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProviderLink)
            .WithMany(x => x.SharePageLinks)
            .HasForeignKey(x => x.ProviderLinkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SharePageId, x.ProviderLinkId }).IsUnique();
    }
}
