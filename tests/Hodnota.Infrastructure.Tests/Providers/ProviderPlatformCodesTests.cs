using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.AppleMusic;
using Hodnota.Infrastructure.Providers.Deezer;
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
    // Every IStreamingProvider in this assembly, found by reflection rather than a hand-kept list, so
    // a new provider is covered automatically instead of depending on someone remembering to add it here.
    private static IEnumerable<Type> ProviderTypes() =>
        typeof(ApplicationDbContext).Assembly.GetTypes()
            .Where(type => typeof(IStreamingProvider).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface);

    // The properties under test are plain initializers, so each constructor's arguments are never used.
    public static IEnumerable<object[]> Providers() =>
    [
        .. ProviderTypes().Select(type =>
        {
            var constructor = type.GetConstructors().Single();
            var provider = (IStreamingProvider)constructor.Invoke(new object?[constructor.GetParameters().Length]);
            return new object[] { provider };
        }),
    ];

    // Reflection finds whatever providers exist; this pins the count so a provider that was meant to
    // implement IStreamingProvider but doesn't (a typo in the interface list) is still caught.
    [Fact]
    public void ProviderTypes_FindsEveryKnownProvider()
    {
        ProviderTypes().Select(type => type.Name).Should().BeEquivalentTo(
        [
            nameof(AppleMusicStreamingProvider),
            nameof(DeezerStreamingProvider),
            nameof(DiscogsStreamingProvider),
            nameof(QobuzStreamingProvider),
            nameof(SpotifyStreamingProvider),
            nameof(TidalStreamingProvider),
            nameof(YouTubeStreamingProvider),
        ]);
    }

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
