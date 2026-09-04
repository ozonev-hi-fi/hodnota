using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>();

        builder.HasOne(x => x.Label)
            .WithMany(x => x.Releases)
            .HasForeignKey(x => x.LabelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Upc).IsUnique().HasFilter("\"Upc\" IS NOT NULL");
    }
}
