using AwesomeAssertions;

using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Identity;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class ApplicationDbContextCatalogTests
{
    private static readonly IEnumerable<string?> ExpectedCatalogTableNames =
    [
        "Artists",
        "Releases",
        "Tracks",
        "ReleaseTracks",
        "ArtistCredits",
        "Platforms",
        "ProviderLinks",
        "Genres",
        "EntityGenres",
        "RecordLabels",
    ];

    private static async Task<(SqliteConnection Connection, ApplicationDbContext Context)> CreateContextAsync(
        TimestampsInterceptor? interceptor = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection);
        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        var context = new ApplicationDbContext(optionsBuilder.Options);
        await context.Database.EnsureCreatedAsync();
        return (connection, context);
    }

    private static Artist NewArtist(string name = "Artist") => new() { Name = name };

    private static Release NewRelease(string title = "Release") => new() { Title = title, Type = ReleaseType.Album };

    private static Track NewTrack(string title = "Track") => new() { Title = title };

    [Fact]
    public async Task EnsureCreated_BuildsAllCatalogTables()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = new ApplicationDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var tableNames = context.Model.GetEntityTypes().Select(entityType => entityType.GetTableName());
        tableNames.Should().Contain(ExpectedCatalogTableNames);
    }

    [Fact]
    public async Task Artist_CanBeInsertedAndQueriedByGuidKey()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var artist = NewArtist();
        artist.Type = ArtistType.Group;
        context.Artists.Add(artist);
        await context.SaveChangesAsync();

        var found = await context.Artists.FindAsync(artist.Id);
        found.Should().NotBeNull();
        found!.Type.Should().Be(ArtistType.Group);
    }

    [Fact]
    public async Task Release_LabelDeleted_SetsLabelIdNull()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var label = new RecordLabel { Name = "Label" };
        var release = NewRelease();
        release.Label = label;
        context.RecordLabels.Add(label);
        context.Releases.Add(release);
        await context.SaveChangesAsync();

        context.RecordLabels.Remove(label);
        await context.SaveChangesAsync();

        var found = await context.Releases.FindAsync(release.Id);
        found!.LabelId.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseTrack_DuplicateDiscAndTrackNumber_ViolatesUniqueConstraint()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var release = NewRelease();
        var track1 = NewTrack("Track 1");
        var track2 = NewTrack("Track 2");
        context.AddRange(release, track1, track2);
        context.ReleaseTracks.Add(new ReleaseTrack { Release = release, Track = track1, DiscNumber = 1, TrackNumber = 1 });
        await context.SaveChangesAsync();

        context.ReleaseTracks.Add(new ReleaseTrack { Release = release, Track = track2, DiscNumber = 1, TrackNumber = 1 });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ArtistCredit_NotExactlyOneTarget_ViolatesCheckConstraint(bool setRelease, bool setTrack)
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var artist = NewArtist();
        var release = NewRelease();
        var track = NewTrack();
        context.AddRange(artist, release, track);
        await context.SaveChangesAsync();

        context.ArtistCredits.Add(new ArtistCredit
        {
            Artist = artist,
            Release = setRelease ? release : null,
            Track = setTrack ? track : null,
            Role = CreditRole.MainArtist,
        });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ArtistCredit_SecondMainArtistOnSameRelease_ViolatesUniqueIndex()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var release = NewRelease();
        var artist1 = NewArtist("Artist 1");
        var artist2 = NewArtist("Artist 2");
        context.AddRange(release, artist1, artist2);
        context.ArtistCredits.Add(new ArtistCredit { Artist = artist1, Release = release, Role = CreditRole.MainArtist });
        await context.SaveChangesAsync();

        context.ArtistCredits.Add(new ArtistCredit { Artist = artist2, Release = release, Role = CreditRole.MainArtist });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ArtistCredit_MultipleNonMainArtistCreditsOnSameRelease_Allowed()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var release = NewRelease();
        var producer = NewArtist("Producer");
        var composer = NewArtist("Composer");
        context.AddRange(
            release,
            new ArtistCredit { Artist = producer, Release = release, Role = CreditRole.Producer },
            new ArtistCredit { Artist = composer, Release = release, Role = CreditRole.Composer });

        var act = () => context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task ProviderLink_NotExactlyOneTarget_ViolatesCheckConstraint(bool setArtist, bool setRelease, bool setTrack)
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var artist = NewArtist();
        var release = NewRelease();
        var track = NewTrack();
        var platform = await context.Platforms.FirstAsync();
        context.AddRange(artist, release, track);
        await context.SaveChangesAsync();

        context.ProviderLinks.Add(new ProviderLink
        {
            Artist = setArtist ? artist : null,
            Release = setRelease ? release : null,
            Track = setTrack ? track : null,
            Platform = platform,
            ExternalId = "id",
            ExternalUrl = new Uri("https://example.com"),
        });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ProviderLink_DuplicatePlatformAndArtist_ViolatesUniqueIndex()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var artist = NewArtist();
        var platform = await context.Platforms.FirstAsync();
        context.Add(artist);
        context.ProviderLinks.Add(new ProviderLink { Artist = artist, Platform = platform, ExternalId = "id-1", ExternalUrl = new Uri("https://example.com/1") });
        await context.SaveChangesAsync();

        context.ProviderLinks.Add(new ProviderLink { Artist = artist, Platform = platform, ExternalId = "id-2", ExternalUrl = new Uri("https://example.com/2") });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task EntityGenre_NotExactlyOneTarget_ViolatesCheckConstraint(bool setArtist, bool setRelease, bool setTrack)
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var artist = NewArtist();
        var release = NewRelease();
        var track = NewTrack();
        var genre = new Genre { Name = "Genre", Slug = "genre" };
        context.AddRange(artist, release, track, genre);
        await context.SaveChangesAsync();

        context.EntityGenres.Add(new EntityGenre
        {
            Genre = genre,
            Artist = setArtist ? artist : null,
            Release = setRelease ? release : null,
            Track = setTrack ? track : null,
        });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task EntityGenre_DuplicateGenreAndRelease_ViolatesUniqueIndex()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var release = NewRelease();
        var genre = new Genre { Name = "Genre", Slug = "genre" };
        context.AddRange(release, genre);
        context.EntityGenres.Add(new EntityGenre { Genre = genre, Release = release });
        await context.SaveChangesAsync();

        context.EntityGenres.Add(new EntityGenre { Genre = genre, Release = release });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Release_DuplicateUpc_ViolatesUniqueIndex()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        context.Releases.Add(new Release { Title = "Release 1", Type = ReleaseType.Album, Upc = "123456789012" });
        await context.SaveChangesAsync();

        context.Releases.Add(new Release { Title = "Release 2", Type = ReleaseType.Album, Upc = "123456789012" });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Release_NullUpcTwice_Allowed()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        context.Releases.Add(new Release { Title = "Release 1", Type = ReleaseType.Single });
        context.Releases.Add(new Release { Title = "Release 2", Type = ReleaseType.Single });
        var act = () => context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Track_DuplicateIsrc_ViolatesUniqueIndex()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        context.Tracks.Add(new Track { Title = "Track 1", Isrc = "USRC12345678" });
        await context.SaveChangesAsync();

        context.Tracks.Add(new Track { Title = "Track 2", Isrc = "USRC12345678" });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Platform_SeedData_ContainsAllSevenKnownPlatforms()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var codes = await context.Platforms.Select(p => p.Code).ToListAsync();

        codes.Should().BeEquivalentTo(
        [
            PlatformCodes.YouTube,
            PlatformCodes.YouTubeMusic,
            PlatformCodes.Qobuz,
            PlatformCodes.Tidal,
            PlatformCodes.Deezer,
            PlatformCodes.AppleMusic,
            PlatformCodes.Bandcamp,
        ]);

        (await context.Platforms.AllAsync(p => p.IsActive)).Should().BeTrue();
    }

    [Fact]
    public async Task Platform_SetInactive_DoesNotAffectExistingProviderLinks()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var platform = await context.Platforms.FirstAsync();
        var artist = NewArtist();
        context.Add(artist);
        context.ProviderLinks.Add(new ProviderLink { Artist = artist, Platform = platform, ExternalId = "id", ExternalUrl = new Uri("https://example.com") });
        await context.SaveChangesAsync();

        platform.IsActive = false;
        await context.SaveChangesAsync();

        var link = await context.ProviderLinks.FirstAsync();
        link.PlatformId.Should().Be(platform.Id);
    }

    [Fact]
    public async Task TimestampsInterceptor_OnInsert_SetsCreatedAndUpdatedToSameUtcNow()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var (connection, context) = await CreateContextAsync(new TimestampsInterceptor(timeProvider));
        await using var _ = connection;
        await using var __ = context;

        var artist = NewArtist();
        context.Artists.Add(artist);
        await context.SaveChangesAsync();

        artist.CreatedAtUtc.Should().Be(timeProvider.GetUtcNow());
        artist.UpdatedAtUtc.Should().Be(timeProvider.GetUtcNow());
    }

    [Fact]
    public async Task TimestampsInterceptor_OnUpdate_AdvancesUpdatedAtUtcOnly()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var (connection, context) = await CreateContextAsync(new TimestampsInterceptor(timeProvider));
        await using var _ = connection;
        await using var __ = context;

        var artist = NewArtist();
        context.Artists.Add(artist);
        await context.SaveChangesAsync();
        var createdAt = artist.CreatedAtUtc;

        timeProvider.Advance(TimeSpan.FromDays(1));
        artist.Name = "Updated";
        await context.SaveChangesAsync();

        artist.CreatedAtUtc.Should().Be(createdAt);
        artist.UpdatedAtUtc.Should().Be(timeProvider.GetUtcNow());
    }
}
