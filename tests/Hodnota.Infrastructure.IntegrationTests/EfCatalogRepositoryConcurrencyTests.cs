using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.IntegrationTests;

// Exercises EfCatalogRepository's retry-on-conflict path against real concurrent Postgres
// connections — SQLite's single-connection-per-test setup elsewhere can't produce a genuine race.
public class EfCatalogRepositoryConcurrencyTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    private DbContextOptions<ApplicationDbContext> Options => new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.ConnectionString).Options;

    [Fact]
    public async Task IsProviderLinkExternalIdConflict_RealPostgresUniqueViolation_ReturnsTrue()
    {
        await using var context = new ApplicationDbContext(Options);
        await context.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var track1 = new Track { Title = $"Track 1 {suffix}" };
        var track2 = new Track { Title = $"Track 2 {suffix}" };
        var platform = await context.Platforms.FirstAsync();
        context.AddRange(track1, track2);
        context.ProviderLinks.Add(new ProviderLink { Track = track1, Platform = platform, ExternalId = suffix, ExternalUrl = new Uri($"https://example.com/{suffix}/1") });
        await context.SaveChangesAsync();

        context.ProviderLinks.Add(new ProviderLink { Track = track2, Platform = platform, ExternalId = suffix, ExternalUrl = new Uri($"https://example.com/{suffix}/2") });
        var act = () => context.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        EfCatalogRepository.IsProviderLinkExternalIdConflict(ex.Which).Should().BeTrue();
    }

    [Fact]
    public async Task CreateSharePageAsync_ConcurrentResolveForSameExternalId_ProducesExactlyOneTrack()
    {
        await using (var migrateContext = new ApplicationDbContext(Options))
        {
            await migrateContext.Database.MigrateAsync();
        }

        var suffix = Guid.NewGuid().ToString("N");
        var title = $"Concurrent Track {suffix}";
        var result = new StreamingSearchResult(
            StreamingResultType.Track,
            title,
            "Artist",
            null,
            [
                new ProviderLinkCandidate(PlatformCodes.YouTube, suffix, new Uri($"https://www.youtube.com/watch?v={suffix}")),
                new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, suffix, new Uri($"https://music.youtube.com/watch?v={suffix}")),
            ]);

        await using var context1 = new ApplicationDbContext(Options);
        await using var context2 = new ApplicationDbContext(Options);
        var repository1 = new EfCatalogRepository(context1);
        var repository2 = new EfCatalogRepository(context2);

        await Task.WhenAll(
            repository1.CreateSharePageAsync(result, CancellationToken.None),
            repository2.CreateSharePageAsync(result, CancellationToken.None));

        await using var verifyContext = new ApplicationDbContext(Options);
        (await verifyContext.Tracks.CountAsync(t => t.Title == title)).Should().Be(1);
        (await verifyContext.ProviderLinks.CountAsync(pl => pl.ExternalId == suffix)).Should().Be(2);
        (await verifyContext.SharePages.CountAsync(sp => sp.Track!.Title == title)).Should().Be(2);
    }
}
