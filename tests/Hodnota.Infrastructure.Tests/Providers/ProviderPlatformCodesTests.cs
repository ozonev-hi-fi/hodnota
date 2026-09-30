using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Providers.Qobuz;
using Hodnota.Infrastructure.Providers.Spotify;
using Hodnota.Infrastructure.Providers.Tidal;
using Hodnota.Infrastructure.Providers.YouTube;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.Tests.Providers;

// A provider whose platform code has no seeded Platform row would be queued for a check on every
// page view and fail every time (the check cannot be saved), so a missing seed is caught here.
public class ProviderPlatformCodesTests
{
    // The properties under test are plain initializers, so no dependency is used.
    public static IEnumerable<object[]> Providers() =>
    [
        [new DiscogsStreamingProvider(null!)],
        [new QobuzStreamingProvider(null!)],
        [new SpotifyStreamingProvider(null!)],
        [new TidalStreamingProvider(null!)],
        [new YouTubeStreamingProvider(null!)],
    ];

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task LinkPlatformCodes_AreAllSeededPlatforms(IStreamingProvider provider)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();

        var seeded = await context.Platforms.Select(p => p.Code).ToListAsync();

        provider.LinkPlatformCodes.Should().NotBeEmpty();
        seeded.Should().Contain(provider.LinkPlatformCodes, $"{provider.ProviderCode} produces links for these platforms");
    }
}
