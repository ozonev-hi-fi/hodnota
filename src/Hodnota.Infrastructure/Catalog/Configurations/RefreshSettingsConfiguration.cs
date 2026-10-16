using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class RefreshSettingsConfiguration : IEntityTypeConfiguration<RefreshSettings>
{
    // The one row every reader/writer uses — RefreshSettingsCache never creates another.
    public static readonly Guid SeedId = Guid.Parse("00000000-0000-0000-0000-00000000a001");

    private static readonly DateTimeOffset SeedTimestamp = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<RefreshSettings> builder)
    {
        builder.Property(x => x.NotFoundRecheckIntervalDays).IsRequired();
        builder.Property(x => x.FoundRecheckIntervalDays).IsRequired();

        builder.HasData(new RefreshSettings
        {
            Id = SeedId,
            NotFoundRecheckIntervalDays = 7,
            FoundRecheckIntervalDays = 90,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
        });
    }
}
