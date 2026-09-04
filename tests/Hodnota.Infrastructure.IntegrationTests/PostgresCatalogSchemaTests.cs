using AwesomeAssertions;

using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;

namespace Hodnota.Infrastructure.IntegrationTests;

// Re-verifies the constraints ApplicationDbContextCatalogTests already checks against SQLite, but
// against real Postgres — per this project's rule that schema-changing features need a real-DB
// pass, not SQLite-only sign-off. See docs/decisions/0007-catalog-data-model.md.
public class PostgresCatalogSchemaTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    private async Task<ApplicationDbContext> CreateMigratedContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    [Fact]
    public async Task Platform_SeedData_PersistsAfterMigrate()
    {
        await using var context = await CreateMigratedContextAsync();

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
    public async Task ArtistCredit_CheckConstraint_RejectsAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        var artist = new Artist { Name = "Artist" };
        context.Artists.Add(artist);
        await context.SaveChangesAsync();

        context.ArtistCredits.Add(new ArtistCredit { Artist = artist, Role = CreditRole.MainArtist });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ArtistCredit_PartialUniqueIndex_RejectsSecondMainArtistAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        var release = new Release { Title = "Release", Type = ReleaseType.Album };
        var artist1 = new Artist { Name = "Artist 1" };
        var artist2 = new Artist { Name = "Artist 2" };
        context.AddRange(release, artist1, artist2);
        context.ArtistCredits.Add(new ArtistCredit { Artist = artist1, Release = release, Role = CreditRole.MainArtist });
        await context.SaveChangesAsync();

        context.ArtistCredits.Add(new ArtistCredit { Artist = artist2, Release = release, Role = CreditRole.MainArtist });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ProviderLink_CheckConstraint_RejectsAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        var platform = await context.Platforms.FirstAsync();
        context.ProviderLinks.Add(new ProviderLink
        {
            Platform = platform,
            ExternalId = "id",
            ExternalUrl = new Uri("https://example.com"),
        });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ProviderLink_PartialUniqueIndex_RejectsDuplicateAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        var artist = new Artist { Name = "Artist" };
        var platform = await context.Platforms.FirstAsync();
        context.Add(artist);
        context.ProviderLinks.Add(new ProviderLink { Artist = artist, Platform = platform, ExternalId = "id-1", ExternalUrl = new Uri("https://example.com/1") });
        await context.SaveChangesAsync();

        context.ProviderLinks.Add(new ProviderLink { Artist = artist, Platform = platform, ExternalId = "id-2", ExternalUrl = new Uri("https://example.com/2") });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task EntityGenre_CheckConstraint_RejectsAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var genre = new Genre { Name = $"Genre {suffix}", Slug = $"genre-{suffix}" };
        context.Genres.Add(genre);
        await context.SaveChangesAsync();

        context.EntityGenres.Add(new EntityGenre { Genre = genre });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task EntityGenre_PartialUniqueIndex_RejectsDuplicateAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var release = new Release { Title = "Release", Type = ReleaseType.Album };
        var genre = new Genre { Name = $"Genre {suffix}", Slug = $"genre-{suffix}" };
        context.AddRange(release, genre);
        context.EntityGenres.Add(new EntityGenre { Genre = genre, Release = release });
        await context.SaveChangesAsync();

        context.EntityGenres.Add(new EntityGenre { Genre = genre, Release = release });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Release_FilteredUpcUniqueIndex_AllowsMultipleNullsRejectsDuplicateAgainstRealPostgres()
    {
        await using var context = await CreateMigratedContextAsync();

        context.Releases.Add(new Release { Title = "No UPC 1", Type = ReleaseType.Single });
        context.Releases.Add(new Release { Title = "No UPC 2", Type = ReleaseType.Single });
        context.Releases.Add(new Release { Title = "Has UPC", Type = ReleaseType.Album, Upc = "123456789012" });
        await context.SaveChangesAsync();

        context.Releases.Add(new Release { Title = "Duplicate UPC", Type = ReleaseType.Album, Upc = "123456789012" });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
