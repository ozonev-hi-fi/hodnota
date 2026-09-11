using AwesomeAssertions;

using Hodnota.Domain.Catalog;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class SharePageSchemaTests
{
    private static async Task<(SqliteConnection Connection, ApplicationDbContext Context)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return (connection, context);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task SharePage_NotExactlyOneTarget_ViolatesCheckConstraint(bool setArtist, bool setRelease, bool setTrack)
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var artist = new Artist { Name = "Artist" };
        var release = new Release { Title = "Release", Type = ReleaseType.Album };
        var track = new Track { Title = "Track" };
        context.AddRange(artist, release, track);
        await context.SaveChangesAsync();

        context.SharePages.Add(new SharePage
        {
            Artist = setArtist ? artist : null,
            Release = setRelease ? release : null,
            Track = setTrack ? track : null,
        });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SharePage_SameTrackCanHaveMultipleSharePages()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var track = new Track { Title = "Track" };
        context.Tracks.Add(track);
        context.SharePages.Add(new SharePage { Track = track });
        context.SharePages.Add(new SharePage { Track = track });

        var act = () => context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SharePage_TrackDeleted_CascadesToSharePage()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var track = new Track { Title = "Track" };
        var sharePage = new SharePage { Track = track };
        context.AddRange(track, sharePage);
        await context.SaveChangesAsync();

        context.Tracks.Remove(track);
        await context.SaveChangesAsync();

        (await context.SharePages.FindAsync(sharePage.Id)).Should().BeNull();
    }

    [Fact]
    public async Task SharePageLink_DuplicateSharePageAndProviderLink_ViolatesUniqueIndex()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var track = new Track { Title = "Track" };
        var platform = await context.Platforms.FirstAsync();
        var providerLink = new ProviderLink { Track = track, Platform = platform, ExternalId = "id", ExternalUrl = new Uri("https://example.com") };
        var sharePage = new SharePage { Track = track };
        context.AddRange(track, providerLink, sharePage);
        context.SharePageLinks.Add(new SharePageLink { SharePage = sharePage, ProviderLink = providerLink });
        await context.SaveChangesAsync();

        context.SharePageLinks.Add(new SharePageLink { SharePage = sharePage, ProviderLink = providerLink });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SharePageLink_IsVisible_DefaultsToTrue()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var track = new Track { Title = "Track" };
        var platform = await context.Platforms.FirstAsync();
        var providerLink = new ProviderLink { Track = track, Platform = platform, ExternalId = "id", ExternalUrl = new Uri("https://example.com") };
        var sharePage = new SharePage { Track = track };
        var sharePageLink = new SharePageLink { SharePage = sharePage, ProviderLink = providerLink };
        context.AddRange(track, providerLink, sharePage, sharePageLink);

        await context.SaveChangesAsync();

        sharePageLink.IsVisible.Should().BeTrue();
    }
}
