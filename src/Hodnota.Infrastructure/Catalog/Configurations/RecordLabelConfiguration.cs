using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class RecordLabelConfiguration : IEntityTypeConfiguration<RecordLabel>
{
    public void Configure(EntityTypeBuilder<RecordLabel> builder)
    {
        builder.Property(x => x.Name).IsRequired();
    }
}
