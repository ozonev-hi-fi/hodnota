using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.IsExplicit).HasDefaultValue(false);

        builder.HasIndex(x => x.Isrc).IsUnique().HasFilter("\"Isrc\" IS NOT NULL");
    }
}
