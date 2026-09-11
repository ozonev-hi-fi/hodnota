using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class EfCatalogRepositoryTests
{
    private static async Task<(SqliteConnection Connection, ApplicationDbContext Context)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return (connection, context);
    }

    private static StreamingSearchResult NewTrackResult(string videoId = "video-1") => new(
        StreamingResultType.Track,
        "Nothing Else Matters",
        "Metallica",
        new Uri("https://example.com/image.jpg"),
        [
            new ProviderLinkCandidate(PlatformCodes.YouTube, videoId, new Uri($"https://www.youtube.com/watch?v={videoId}")),
            new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, videoId, new Uri($"https://music.youtube.com/watch?v={videoId}")),
        ]);

    [Fact]
    public async Task CreateSharePageAsync_NewTrack_CreatesArtistTrackAndBothProviderLinks()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        var result = await repository.CreateSharePageAsync(NewTrackResult(), CancellationToken.None);

        result.Name.Should().Be("Nothing Else Matters");
        result.ArtistName.Should().Be("Metallica");
        result.Links.Should().HaveCount(2);
        result.Links.Should().Contain(l => l.PlatformCode == PlatformCodes.YouTube);
        result.Links.Should().Contain(l => l.PlatformCode == PlatformCodes.YouTubeMusic);
        (await context.Tracks.CountAsync()).Should().Be(1);
        (await context.Artists.CountAsync()).Should().Be(1);
        (await context.ProviderLinks.CountAsync()).Should().Be(2);
        (await context.SharePages.CountAsync()).Should().Be(1);
        (await context.SharePageLinks.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CreateSharePageAsync_SameVideoIdResolvedTwice_ReusesTrackButCreatesSecondSharePage()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        var first = await repository.CreateSharePageAsync(NewTrackResult(), CancellationToken.None);
        var second = await repository.CreateSharePageAsync(NewTrackResult(), CancellationToken.None);

        second.Id.Should().NotBe(first.Id);
        (await context.Tracks.CountAsync()).Should().Be(1);
        (await context.Artists.CountAsync()).Should().Be(1);
        (await context.ProviderLinks.CountAsync()).Should().Be(2);
        (await context.SharePages.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CreateSharePageAsync_DifferentVideoId_CreatesSeparateTrack()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        await repository.CreateSharePageAsync(NewTrackResult("video-1"), CancellationToken.None);
        await repository.CreateSharePageAsync(NewTrackResult("video-2"), CancellationToken.None);

        (await context.Tracks.CountAsync()).Should().Be(2);
        (await context.Artists.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CreateSharePageAsync_ReusedTrack_ReturnsLinksOrderedByCreationNotInsertionOrder()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var track = new Track { Title = "Nothing Else Matters" };
        var artist = new Artist { Name = "Metallica" };
        context.AddRange(track, artist, new ArtistCredit { Artist = artist, Track = track, Role = CreditRole.MainArtist });
        var youTubeMusicPlatform = await context.Platforms.FirstAsync(p => p.Code == PlatformCodes.YouTubeMusic);
        var youTubePlatform = await context.Platforms.FirstAsync(p => p.Code == PlatformCodes.YouTube);
        // Inserted in reverse of their intended display order, with CreatedAtUtc set explicitly (no
        // TimestampsInterceptor here) so only an actual ORDER BY, not insertion order, can produce the
        // expected result.
        context.ProviderLinks.Add(new ProviderLink
        {
            Track = track,
            Platform = youTubeMusicPlatform,
            ExternalId = "video-1",
            ExternalUrl = new Uri("https://music.youtube.com/watch?v=video-1"),
            CreatedAtUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
        });
        context.ProviderLinks.Add(new ProviderLink
        {
            Track = track,
            Platform = youTubePlatform,
            ExternalId = "video-1",
            ExternalUrl = new Uri("https://www.youtube.com/watch?v=video-1"),
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });
        await context.SaveChangesAsync();
        var repository = new EfCatalogRepository(context);

        var result = await repository.CreateSharePageAsync(NewTrackResult(), CancellationToken.None);

        result.Links.Select(l => l.PlatformCode).Should().Equal(PlatformCodes.YouTube, PlatformCodes.YouTubeMusic);
    }

    [Fact]
    public async Task CreateSharePageAsync_NewRelease_CreatesReleaseNotTrack()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var result = new StreamingSearchResult(
            StreamingResultType.Release,
            "Nothing",
            "N.E.R.D.",
            null,
            [
                new ProviderLinkCandidate(PlatformCodes.YouTube, "playlist-1", new Uri("https://www.youtube.com/playlist?list=playlist-1")),
                new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, "playlist-1", new Uri("https://music.youtube.com/playlist?list=playlist-1")),
            ]);

        var sharePageResult = await repository.CreateSharePageAsync(result, CancellationToken.None);

        sharePageResult.Type.Should().Be(StreamingResultType.Release);
        (await context.Releases.CountAsync()).Should().Be(1);
        (await context.Tracks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetSharePageAsync_WithExistingId_ReturnsSharePageWithPlatformType()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var created = await repository.CreateSharePageAsync(NewTrackResult(), CancellationToken.None);

        var result = await repository.GetSharePageAsync(created.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Nothing Else Matters");
        result.ArtistName.Should().Be("Metallica");
        result.Links.Should().HaveCount(2);
        result.Links.Should().OnlyContain(l => l.PlatformType == PlatformType.StreamingService);
    }

    [Fact]
    public async Task GetSharePageAsync_WithUnknownId_ReturnsNull()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        var result = await repository.GetSharePageAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSharePageAsync_ExcludesHiddenLinks_AndOrdersByDisplayOrder()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var created = await repository.CreateSharePageAsync(NewTrackResult(), CancellationToken.None);
        var links = await context.SharePageLinks.Include(l => l.ProviderLink).ThenInclude(pl => pl.Platform)
            .Where(l => l.SharePageId == created.Id).ToListAsync();
        var youTubeLink = links.Single(l => l.ProviderLink.Platform.Code == PlatformCodes.YouTube);
        var youTubeMusicLink = links.Single(l => l.ProviderLink.Platform.Code == PlatformCodes.YouTubeMusic);
        youTubeLink.DisplayOrder = 1;
        youTubeMusicLink.DisplayOrder = 0;
        youTubeMusicLink.IsVisible = false;
        await context.SaveChangesAsync();
        // Filtered Include runs its WHERE clause in SQL, but EF's navigation fixup would otherwise
        // attach the already-tracked (now-hidden) link back onto the result via the change tracker's
        // identity map regardless of the filter — clear it so this test exercises the real SQL filter.
        context.ChangeTracker.Clear();

        var result = await repository.GetSharePageAsync(created.Id, CancellationToken.None);

        result!.Links.Should().ContainSingle();
        result.Links[0].PlatformCode.Should().Be(PlatformCodes.YouTube);
    }
}
