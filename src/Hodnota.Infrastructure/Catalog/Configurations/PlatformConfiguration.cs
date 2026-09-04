using Hodnota.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hodnota.Infrastructure.Catalog.Configurations;

public sealed class PlatformConfiguration : IEntityTypeConfiguration<Platform>
{
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 9, 4, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Platform> builder)
    {
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Code).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>();
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasData(
            Seed("00000000-0000-0000-0000-000000000001", "YouTube", PlatformCodes.YouTube, PlatformType.StreamingService),
            Seed("00000000-0000-0000-0000-000000000002", "YouTube Music", PlatformCodes.YouTubeMusic, PlatformType.StreamingService),
            Seed("00000000-0000-0000-0000-000000000003", "Qobuz", PlatformCodes.Qobuz, PlatformType.StreamingService),
            Seed("00000000-0000-0000-0000-000000000004", "Tidal", PlatformCodes.Tidal, PlatformType.StreamingService),
            Seed("00000000-0000-0000-0000-000000000005", "Deezer", PlatformCodes.Deezer, PlatformType.StreamingService),
            Seed("00000000-0000-0000-0000-000000000006", "Apple Music", PlatformCodes.AppleMusic, PlatformType.StreamingService),
            Seed("00000000-0000-0000-0000-000000000007", "Bandcamp", PlatformCodes.Bandcamp, PlatformType.DigitalStore));
    }

    private static Platform Seed(string id, string name, string code, PlatformType type) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        Code = code,
        Type = type,
        IsActive = true,
        CreatedAtUtc = SeedTimestamp,
        UpdatedAtUtc = SeedTimestamp,
    };
}
