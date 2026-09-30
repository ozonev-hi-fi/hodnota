using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class EfCatalogRepositoryEnrichmentTests
{
    private static async Task<(SqliteConnection Connection, ApplicationDbContext Context)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return (connection, context);
    }

    private static ProviderLinkCandidate Link(string platformCode, string externalId) =>
        new(platformCode, externalId, new Uri($"https://example.com/{platformCode}/{externalId}"));

    private static StreamingSearchResult SpotifyTrack(string externalId = "sp-1", string name = "Nothing Else Matters") => new(
        StreamingResultType.Track,
        name,
        "Metallica",
        null,
        [Link(PlatformCodes.Spotify, externalId)],
        Isrc: "USRC17607839");

    private static ProviderEnrichment Enrichment(string providerCode, LookupOutcome outcome, params ProviderLinkCandidate[] links) =>
        new(providerCode, [providerCode], outcome, links);

    [Fact]
    public async Task GetEnrichmentRequestAsync_ForATrackPage_ReturnsTitleArtistIsrcAndStoredLinks()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);

        var request = await repository.GetEnrichmentRequestAsync(page.Id, CancellationToken.None);

        request.Should().NotBeNull();
        request!.Type.Should().Be(StreamingResultType.Track);
        request.Name.Should().Be("Nothing Else Matters");
        request.ArtistName.Should().Be("Metallica");
        request.Isrc.Should().Be("USRC17607839");
        request.Upc.Should().BeNull();
        request.ExistingLinks.Should().ContainSingle().Which.Should().BeEquivalentTo(Link(PlatformCodes.Spotify, "sp-1"));
    }

    [Fact]
    public async Task GetEnrichmentRequestAsync_ForAReleasePage_ReturnsTheUpc()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var album = new StreamingSearchResult(
            StreamingResultType.Release, "OK Computer", "Radiohead", null, [Link(PlatformCodes.Qobuz, "q1")], Upc: "0724385522925");
        var page = await repository.ResolveSharePageAsync(album, CancellationToken.None);

        var request = await repository.GetEnrichmentRequestAsync(page.Id, CancellationToken.None);

        request!.Type.Should().Be(StreamingResultType.Release);
        request.Upc.Should().Be("724385522925", "the stored form drops the extra leading zero");
        request.Isrc.Should().BeNull();
    }

    [Fact]
    public async Task GetEnrichmentRequestAsync_WithUnknownId_ReturnsNull()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        (await repository.GetEnrichmentRequestAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SaveEnrichmentAsync_ExactMatchOnANewPlatform_AddsTheLinkTheCheckAndTheSharePageLink()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);

        var saved = await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.ExactMatch, Link(PlatformCodes.Tidal, "t-1")), CancellationToken.None);

        saved.Should().ContainSingle();
        saved[0].PlatformCode.Should().Be(PlatformCodes.Tidal);
        saved[0].Outcome.Should().Be(LookupOutcome.ExactMatch);
        saved[0].Url.Should().Be(new Uri("https://example.com/tidal/t-1"));
        var tidalLink = await context.ProviderLinks.Include(pl => pl.Platform).SingleAsync(pl => pl.Platform.Code == PlatformCodes.Tidal);
        tidalLink.Confidence.Should().Be(1.0);
        tidalLink.LastVerifiedUtc.Should().NotBeNull();
        var check = await context.ProviderChecks.Include(c => c.Platform).SingleAsync();
        check.Platform.Code.Should().Be(PlatformCodes.Tidal);
        check.Outcome.Should().Be(LookupOutcome.ExactMatch);
        check.CheckedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        context.ChangeTracker.Clear();
        var reloaded = await repository.GetSharePageAsync(page.Id, CancellationToken.None);
        reloaded!.Links.Select(l => l.PlatformCode).Should().Equal(PlatformCodes.Spotify, PlatformCodes.Tidal);
    }

    [Fact]
    public async Task SaveEnrichmentAsync_ExactMatchWithADifferentIdThanTheNameMatch_ReplacesTheLinkInPlace()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack("sp-remaster"), CancellationToken.None);
        var linkId = await context.ProviderLinks.Select(pl => pl.Id).SingleAsync();

        var saved = await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Spotify, LookupOutcome.ExactMatch, Link(PlatformCodes.Spotify, "sp-original")), CancellationToken.None);

        saved.Single().Outcome.Should().Be(LookupOutcome.ExactMatch);
        var link = await context.ProviderLinks.SingleAsync();
        link.Id.Should().Be(linkId);
        link.ExternalId.Should().Be("sp-original");
        link.ExternalUrl.Should().Be(new Uri("https://example.com/spotify/sp-original"));
        link.Confidence.Should().Be(1.0);
        (await context.SharePageLinks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SaveEnrichmentAsync_NameMatchOfTheStoredLink_SetsHalfConfidenceAndKeepsTheLink()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);

        var saved = await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Spotify, LookupOutcome.NameMatch, Link(PlatformCodes.Spotify, "sp-1")), CancellationToken.None);

        saved.Single().Outcome.Should().Be(LookupOutcome.NameMatch);
        var link = await context.ProviderLinks.SingleAsync();
        link.ExternalId.Should().Be("sp-1");
        link.Confidence.Should().Be(0.5);
        link.LastVerifiedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveEnrichmentAsync_NameMatchWithADifferentId_DoesNotOverwriteAnExactLink()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Spotify, LookupOutcome.ExactMatch, Link(PlatformCodes.Spotify, "sp-1")), CancellationToken.None);

        var saved = await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Spotify, LookupOutcome.NameMatch, Link(PlatformCodes.Spotify, "sp-other")), CancellationToken.None);

        saved.Single().Outcome.Should().Be(LookupOutcome.NameMatch);
        var link = await context.ProviderLinks.SingleAsync();
        link.ExternalId.Should().Be("sp-1");
        link.Confidence.Should().Be(1.0);
    }

    [Theory]
    [InlineData(LookupOutcome.NotFound)]
    [InlineData(LookupOutcome.Failed)]
    public async Task SaveEnrichmentAsync_NotFoundOrFailed_RecordsTheCheckAndAddsNoLink(LookupOutcome outcome)
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);

        var saved = await repository.SaveEnrichmentAsync(page.Id, Enrichment(ProviderCodes.Tidal, outcome), CancellationToken.None);

        saved.Single().Outcome.Should().Be(outcome);
        saved.Single().Url.Should().BeNull();
        (await context.ProviderLinks.CountAsync()).Should().Be(1);
        (await context.ProviderChecks.Select(c => c.Outcome).SingleAsync()).Should().Be(outcome);
    }

    [Fact]
    public async Task SaveEnrichmentAsync_FailedAndThenChecked_UpdatesTheSameCheckRow()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.Failed), CancellationToken.None);

        await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.ExactMatch, Link(PlatformCodes.Tidal, "t-1")), CancellationToken.None);

        (await context.ProviderChecks.CountAsync()).Should().Be(1);
        (await context.ProviderChecks.Select(c => c.Outcome).SingleAsync()).Should().Be(LookupOutcome.ExactMatch);
    }

    [Fact]
    public async Task SaveEnrichmentAsync_LinkAlreadyOwnedByAnotherEntity_IsNotAddedAndIsRecordedAsNotFound()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var other = new StreamingSearchResult(
            StreamingResultType.Track, "Enter Sandman", "Metallica", null, [Link(PlatformCodes.Tidal, "t-1")]);
        await repository.ResolveSharePageAsync(other, CancellationToken.None);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);

        var saved = await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.ExactMatch, Link(PlatformCodes.Tidal, "t-1")), CancellationToken.None);

        saved.Single().Outcome.Should().Be(LookupOutcome.NotFound);
        (await context.ProviderLinks.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SaveEnrichmentAsync_TwoCandidatesForOnePlatform_KeepsTheFirst()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);

        await repository.SaveEnrichmentAsync(
            page.Id,
            Enrichment(ProviderCodes.Tidal, LookupOutcome.ExactMatch, Link(PlatformCodes.Tidal, "t-1"), Link(PlatformCodes.Tidal, "t-2")),
            CancellationToken.None);

        (await context.ProviderLinks.Include(pl => pl.Platform).Where(pl => pl.Platform.Code == PlatformCodes.Tidal).Select(pl => pl.ExternalId).ToListAsync())
            .Should().Equal("t-1");
    }

    [Fact]
    public async Task SaveEnrichmentAsync_NewLink_IsAddedToEverySharePageOfTheItem()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        var trackId = await context.Tracks.Select(t => t.Id).SingleAsync();
        var olderPage = new SharePage { TrackId = trackId };
        context.SharePages.Add(olderPage);
        await context.SaveChangesAsync();

        await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.ExactMatch, Link(PlatformCodes.Tidal, "t-1")), CancellationToken.None);

        (await context.SharePageLinks.CountAsync(l => l.SharePageId == page.Id)).Should().Be(2);
        (await context.SharePageLinks.CountAsync(l => l.SharePageId == olderPage.Id)).Should().Be(1);
    }

    [Fact]
    public async Task SaveEnrichmentAsync_WithUnknownId_ReturnsNothing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);

        var saved = await repository.SaveEnrichmentAsync(Guid.NewGuid(), Enrichment(ProviderCodes.Tidal, LookupOutcome.NotFound), CancellationToken.None);

        saved.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlatformChecksAsync_ReturnsEachCheckWithTheStoredLinkUrl()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.ExactMatch, Link(PlatformCodes.Tidal, "t-1")), CancellationToken.None);
        await repository.SaveEnrichmentAsync(page.Id, Enrichment(ProviderCodes.Qobuz, LookupOutcome.Failed), CancellationToken.None);

        var checks = await repository.GetPlatformChecksAsync(page.Id, CancellationToken.None);

        checks.Should().BeEquivalentTo(
        [
            new PlatformCheckResult(PlatformCodes.Tidal, LookupOutcome.ExactMatch, new Uri("https://example.com/tidal/t-1")),
            new PlatformCheckResult(PlatformCodes.Qobuz, LookupOutcome.Failed, null),
        ]);
    }

    [Fact]
    public async Task GetPlatformChecksAsync_ForAPageNeverChecked_ReturnsNothing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);

        (await repository.GetPlatformChecksAsync(page.Id, CancellationToken.None)).Should().BeEmpty();
    }
}
