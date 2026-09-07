using AwesomeAssertions;

using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.IntegrationTests;

// Re-verifies the SharePage/SharePageLink constraints SharePageSchemaTests already checks against
// SQLite, but against real Postgres.
public class PostgresSharePageSchemaTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    private async Task<ApplicationDbContext> CreateMigratedContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    [Fact]
    public async Task SharePage_CheckConstraint_RejectsAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        context.SharePages.Add(new SharePage());
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SharePageLink_UniqueIndex_RejectsDuplicateAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var track = new Track { Title = $"Track {suffix}" };
        var platform = await context.Platforms.FirstAsync();
        var providerLink = new ProviderLink { Track = track, Platform = platform, ExternalId = suffix, ExternalUrl = new Uri($"https://example.com/{suffix}") };
        var sharePage = new SharePage { Track = track };
        context.AddRange(track, providerLink, sharePage);
        context.SharePageLinks.Add(new SharePageLink { SharePage = sharePage, ProviderLink = providerLink });
        await context.SaveChangesAsync();

        context.SharePageLinks.Add(new SharePageLink { SharePage = sharePage, ProviderLink = providerLink });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
