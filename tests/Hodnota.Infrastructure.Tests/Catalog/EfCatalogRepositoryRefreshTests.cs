using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Hodnota.Infrastructure.Tests.Catalog;

public class EfCatalogRepositoryRefreshTests
{
    private static readonly TimeSpan NotFoundInterval = TimeSpan.FromDays(7);
    private static readonly TimeSpan FoundInterval = TimeSpan.FromDays(90);
    private static readonly IReadOnlyList<string> InScopePlatformCodes = [PlatformCodes.Tidal, PlatformCodes.Spotify, PlatformCodes.Discogs];

    private static async Task<(SqliteConnection Connection, ApplicationDbContext Context, FakeTimeProvider Clock)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return (connection, context, new FakeTimeProvider(DateTimeOffset.UtcNow));
    }

    private static ProviderLinkCandidate Link(string platformCode, string externalId) =>
        new(platformCode, externalId, new Uri($"https://example.com/{platformCode}/{externalId}"));

    private static StreamingSearchResult SpotifyTrack(string externalId = "sp-1") => new(
        StreamingResultType.Track, "Nothing Else Matters", "Metallica", null, [Link(PlatformCodes.Spotify, externalId)]);

    private static ProviderEnrichment Enrichment(string providerCode, LookupOutcome outcome, params ProviderLinkCandidate[] links) =>
        new(providerCode, [providerCode], outcome, links, Confirmed: true);

    [Fact]
    public async Task NotFoundCheckPastTheInterval_IsDue()
    {
        var (connection, context, clock) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context, clock);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.NotFound), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(8));

        var due = await repository.GetDueProviderChecksAsync(InScopePlatformCodes, NotFoundInterval, FoundInterval, 50, CancellationToken.None);

        due.Should().ContainSingle(c => c.SharePageId == page.Id && c.PlatformCode == PlatformCodes.Tidal);
    }

    [Fact]
    public async Task NotFoundCheckWithinTheInterval_IsNotDue()
    {
        var (connection, context, clock) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context, clock);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.NotFound), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(1));

        var due = await repository.GetDueProviderChecksAsync(InScopePlatformCodes, NotFoundInterval, FoundInterval, 50, CancellationToken.None);

        due.Should().BeEmpty();
    }

    [Fact]
    public async Task ExactMatchCheckPastTheInterval_IsDue()
    {
        var (connection, context, clock) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context, clock);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.ExactMatch, Link(PlatformCodes.Tidal, "t-1")), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(91));

        var due = await repository.GetDueProviderChecksAsync(InScopePlatformCodes, NotFoundInterval, FoundInterval, 50, CancellationToken.None);

        due.Should().ContainSingle(c => c.SharePageId == page.Id && c.PlatformCode == PlatformCodes.Tidal);
    }

    [Fact]
    public async Task ExactMatchCheckWithinTheInterval_IsNotDue()
    {
        var (connection, context, clock) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context, clock);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(
            page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.ExactMatch, Link(PlatformCodes.Tidal, "t-1")), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(10));

        var due = await repository.GetDueProviderChecksAsync(InScopePlatformCodes, NotFoundInterval, FoundInterval, 50, CancellationToken.None);

        due.Should().BeEmpty();
    }

    [Fact]
    public async Task PlatformNotInTheRequestedList_IsNeverReturnedEvenWhenStale()
    {
        var (connection, context, clock) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context, clock);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(page.Id, Enrichment(ProviderCodes.YouTube, LookupOutcome.NotFound), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(365));

        var due = await repository.GetDueProviderChecksAsync(InScopePlatformCodes, NotFoundInterval, FoundInterval, 50, CancellationToken.None);

        due.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateRefreshIntervalsAsync_ThenGetRefreshIntervalsAsync_ReturnsTheUpdatedValues()
    {
        var (connection, context, clock) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context, clock);

        await repository.UpdateRefreshIntervalsAsync(new RefreshIntervals(3, 30), CancellationToken.None);
        var intervals = await repository.GetRefreshIntervalsAsync(CancellationToken.None);

        intervals.Should().Be(new RefreshIntervals(3, 30));
    }

    [Fact]
    public async Task FailedCheckPastTheNotFoundInterval_IsDue()
    {
        var (connection, context, clock) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context, clock);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.Failed), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(8));

        var due = await repository.GetDueProviderChecksAsync(InScopePlatformCodes, NotFoundInterval, FoundInterval, 50, CancellationToken.None);

        due.Should().ContainSingle(c => c.SharePageId == page.Id && c.PlatformCode == PlatformCodes.Tidal);
    }

    [Fact]
    public async Task FailedCheckWithinTheNotFoundInterval_IsNotDue()
    {
        var (connection, context, clock) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var repository = new EfCatalogRepository(context, clock);
        var page = await repository.ResolveSharePageAsync(SpotifyTrack(), CancellationToken.None);
        await repository.SaveEnrichmentAsync(page.Id, Enrichment(ProviderCodes.Tidal, LookupOutcome.Failed), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(1));

        var due = await repository.GetDueProviderChecksAsync(InScopePlatformCodes, NotFoundInterval, FoundInterval, 50, CancellationToken.None);

        due.Should().BeEmpty();
    }
}
