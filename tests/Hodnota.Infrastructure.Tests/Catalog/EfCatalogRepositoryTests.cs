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
    public async Task ResolveSharePageAsync_NewTrack_CreatesArtistTrackAndBothProviderLinks()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        var result = await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);

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
    public async Task ResolveSharePageAsync_SameVideoIdResolvedTwice_ReusesTrackAndSharePage()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        var first = await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);
        var second = await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);

        second.Id.Should().Be(first.Id);
        second.Links.Should().HaveCount(2);
        (await context.Tracks.CountAsync()).Should().Be(1);
        (await context.Artists.CountAsync()).Should().Be(1);
        (await context.ProviderLinks.CountAsync()).Should().Be(2);
        (await context.SharePages.CountAsync()).Should().Be(1);
        (await context.SharePageLinks.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ResolveSharePageAsync_ItemWithSeveralOlderSharePages_ReturnsTheOldestOne()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var first = await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);
        var trackId = await context.Tracks.Select(t => t.Id).SingleAsync();
        // No TimestampsInterceptor in this context, so both dates are set explicitly.
        (await context.SharePages.SingleAsync()).CreatedAtUtc = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var older = new SharePage
        {
            TrackId = trackId,
            CreatedAtUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        context.SharePages.Add(older);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);

        result.Id.Should().Be(older.Id);
        result.Id.Should().NotBe(first.Id);
        result.Links.Should().HaveCount(2, "links missing from an older page are added to it");
    }

    [Fact]
    public async Task ResolveSharePageAsync_EntityAlreadyHasALinkOnThePlatform_KeepsItInsteadOfAddingASecond()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var first = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-1", new Uri("https://open.spotify.com/track/sp-1"))],
            Isrc: "USRC17607839");
        await repository.ResolveSharePageAsync(first, CancellationToken.None);

        // The same recording (same ISRC), but the provider now returns a different Spotify id.
        var second = first with { Links = [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-2", new Uri("https://open.spotify.com/track/sp-2"))] };

        var act = () => repository.ResolveSharePageAsync(second, CancellationToken.None);

        await act.Should().NotThrowAsync();
        (await context.ProviderLinks.Select(pl => pl.ExternalId).ToListAsync()).Should().Equal("sp-1");
    }

    [Fact]
    public async Task ResolveSharePageAsync_DifferentVideoId_CreatesSeparateTrack()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        await repository.ResolveSharePageAsync(NewTrackResult("video-1"), CancellationToken.None);
        await repository.ResolveSharePageAsync(NewTrackResult("video-2"), CancellationToken.None);

        (await context.Tracks.CountAsync()).Should().Be(2);
        (await context.Artists.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ResolveSharePageAsync_ReusedTrack_ReturnsLinksOrderedByCreationNotInsertionOrder()
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

        var result = await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);

        result.Links.Select(l => l.PlatformCode).Should().Equal(PlatformCodes.YouTube, PlatformCodes.YouTubeMusic);
    }

    [Fact]
    public async Task ResolveSharePageAsync_NewRelease_CreatesReleaseNotTrack()
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

        var sharePageResult = await repository.ResolveSharePageAsync(result, CancellationToken.None);

        sharePageResult.Type.Should().Be(StreamingResultType.Release);
        (await context.Releases.CountAsync()).Should().Be(1);
        (await context.Tracks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ResolveSharePageAsync_ExistingEntityAndANewPlatformLink_AddsTheMissingLink()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var youTubeOnly = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [new ProviderLinkCandidate(PlatformCodes.YouTube, "video-1", new Uri("https://www.youtube.com/watch?v=video-1"))]);
        await repository.ResolveSharePageAsync(youTubeOnly, CancellationToken.None);

        // Simulates a merged candidate: the same YouTube link this track already has, plus a
        // brand-new Spotify link a later search discovered for the same song. See ADR 0011.
        var mergedWithSpotify = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [
                new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-1", new Uri("https://open.spotify.com/track/sp-1")),
                new ProviderLinkCandidate(PlatformCodes.YouTube, "video-1", new Uri("https://www.youtube.com/watch?v=video-1")),
            ]);

        var result = await repository.ResolveSharePageAsync(mergedWithSpotify, CancellationToken.None);

        result.Links.Select(l => l.PlatformCode).Should().Contain(PlatformCodes.Spotify);
        result.Links.Should().HaveCount(2);
        (await context.Tracks.CountAsync()).Should().Be(1);
        (await context.ProviderLinks.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ResolveSharePageAsync_LinkAlreadyOwnedByADifferentEntity_SkipsItWithoutThrowing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var entityA = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-1", new Uri("https://open.spotify.com/track/sp-1"))]);
        var entityB = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters (Live)",
            "Metallica",
            null,
            [new ProviderLinkCandidate(PlatformCodes.YouTube, "video-1", new Uri("https://www.youtube.com/watch?v=video-1"))]);
        await repository.ResolveSharePageAsync(entityA, CancellationToken.None);
        await repository.ResolveSharePageAsync(entityB, CancellationToken.None);

        // Straddles both pre-existing entities: the Spotify link belongs to entityA, the YouTube
        // link to entityB. Resolves into entityA (the first candidate link that already exists)
        // and must skip the YouTube link rather than duplicate it onto entityA or crash.
        var straddling = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [
                new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-1", new Uri("https://open.spotify.com/track/sp-1")),
                new ProviderLinkCandidate(PlatformCodes.YouTube, "video-1", new Uri("https://www.youtube.com/watch?v=video-1")),
            ]);

        var result = await repository.ResolveSharePageAsync(straddling, CancellationToken.None);

        result.Links.Should().ContainSingle(l => l.PlatformCode == PlatformCodes.Spotify);
        (await context.Tracks.CountAsync()).Should().Be(2);
        (await context.ProviderLinks.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ResolveSharePageAsync_NewTrackWithIsrc_PersistsIsrc()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var result = NewTrackResult() with { Isrc = "USRC17607839" };

        await repository.ResolveSharePageAsync(result, CancellationToken.None);

        (await context.Tracks.Select(t => t.Isrc).SingleAsync()).Should().Be("USRC17607839");
    }

    [Fact]
    public async Task ResolveSharePageAsync_NewReleaseWithUpc_PersistsUpc()
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
            [new ProviderLinkCandidate(PlatformCodes.Qobuz, "album-1", new Uri("https://open.qobuz.com/album/album-1"))],
            Upc: "042284197928");

        await repository.ResolveSharePageAsync(result, CancellationToken.None);

        (await context.Releases.Select(r => r.Upc).SingleAsync()).Should().Be("042284197928");
    }

    [Fact]
    public async Task ResolveSharePageAsync_SameIsrcFromAnUnlinkedProvider_ReusesTheTrackAndItsSharePage()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var first = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [new ProviderLinkCandidate(PlatformCodes.Qobuz, "1", new Uri("https://open.qobuz.com/track/1"))],
            Isrc: "USRC17607839");
        var firstPage = await repository.ResolveSharePageAsync(first, CancellationToken.None);

        // No shared ProviderLinkCandidate with the first result, only the same ISRC.
        var second = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-1", new Uri("https://open.spotify.com/track/sp-1"))],
            Isrc: "usrc-17-607839");

        var secondPage = await repository.ResolveSharePageAsync(second, CancellationToken.None);

        secondPage.Id.Should().Be(firstPage.Id);
        secondPage.Links.Select(l => l.PlatformCode).Should().BeEquivalentTo([PlatformCodes.Qobuz, PlatformCodes.Spotify]);
        (await context.Tracks.CountAsync()).Should().Be(1);
        (await context.Tracks.Select(t => t.Isrc).SingleAsync()).Should().Be("USRC17607839");
    }

    [Fact]
    public async Task ResolveSharePageAsync_ExistingTrackWithoutIsrc_GetsTheIsrcOfALaterResolve()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);

        await repository.ResolveSharePageAsync(NewTrackResult() with { Isrc = "USRC17607839" }, CancellationToken.None);

        (await context.Tracks.Select(t => t.Isrc).SingleAsync()).Should().Be("USRC17607839");
    }

    [Fact]
    public async Task ResolveSharePageAsync_SameUpcInAnotherFormFromAnUnlinkedProvider_ReusesTheRelease()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var first = new StreamingSearchResult(
            StreamingResultType.Release,
            "Nothing",
            "N.E.R.D.",
            null,
            [new ProviderLinkCandidate(PlatformCodes.Qobuz, "album-1", new Uri("https://open.qobuz.com/album/album-1"))],
            Upc: "042284197928");
        var firstPage = await repository.ResolveSharePageAsync(first, CancellationToken.None);

        // No shared ProviderLinkCandidate with the first result, only the same UPC written as EAN-13.
        var second = new StreamingSearchResult(
            StreamingResultType.Release,
            "Nothing",
            "N.E.R.D.",
            null,
            [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-album-1", new Uri("https://open.spotify.com/album/sp-album-1"))],
            Upc: "0042284197928");

        var secondPage = await repository.ResolveSharePageAsync(second, CancellationToken.None);

        secondPage.Id.Should().Be(firstPage.Id);
        (await context.Releases.CountAsync()).Should().Be(1);
        (await context.Releases.Select(r => r.Upc).SingleAsync()).Should().Be("042284197928");
    }

    [Fact]
    public async Task GetSharePageAsync_WithExistingId_ReturnsSharePageWithPlatformType()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var created = await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);

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
        var created = await repository.ResolveSharePageAsync(NewTrackResult(), CancellationToken.None);
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
