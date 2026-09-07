using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Identity;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hodnota.Infrastructure.Catalog;

public sealed class EfCatalogRepository(ApplicationDbContext dbContext) : ICatalogRepository
{
    private const string ProviderLinkExternalIdIndexName = "IX_ProviderLinks_PlatformId_ExternalId";

    public async Task<SharePageResult> CreateSharePageAsync(StreamingSearchResult result, CancellationToken cancellationToken)
    {
        try
        {
            return await CreateSharePageCoreAsync(result, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsProviderLinkExternalIdConflict(ex))
        {
            // A concurrent resolve for the same external ID committed first, tripping the unique index
            // between our lookup and our insert — retry once so the now-committed row is found and reused.
            dbContext.ChangeTracker.Clear();
            return await CreateSharePageCoreAsync(result, cancellationToken);
        }
    }

    internal static bool IsProviderLinkExternalIdConflict(DbUpdateException ex) => ex.InnerException switch
    {
        PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == ProviderLinkExternalIdIndexName,
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode == 2067 && sqlite.Message.Contains("ProviderLinks.PlatformId, ProviderLinks.ExternalId"),
        _ => false,
    };

    private async Task<SharePageResult> CreateSharePageCoreAsync(StreamingSearchResult result, CancellationToken cancellationToken)
    {
        var platformsByCode = await dbContext.Platforms
            .Where(p => result.Links.Select(l => l.PlatformCode).Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, cancellationToken);

        var existingLink = await FindExistingProviderLinkAsync(result.Links, platformsByCode, cancellationToken);

        var resolution = result.Type == StreamingResultType.Track
            ? await ResolveTrackAsync(result, existingLink?.TrackId, platformsByCode, cancellationToken)
            : await ResolveReleaseAsync(result, existingLink?.ReleaseId, platformsByCode, cancellationToken);

        var sharePage = result.Type == StreamingResultType.Track
            ? new SharePage { TrackId = resolution.EntityId }
            : new SharePage { ReleaseId = resolution.EntityId };
        dbContext.SharePages.Add(sharePage);

        for (var order = 0; order < resolution.ProviderLinks.Count; order++)
        {
            dbContext.SharePageLinks.Add(new SharePageLink { SharePage = sharePage, ProviderLink = resolution.ProviderLinks[order], DisplayOrder = order });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SharePageResult(
            sharePage.Id,
            result.Type,
            resolution.Name,
            resolution.ArtistName,
            [.. resolution.ProviderLinks.Select(pl => new SharePageLinkResult(pl.Platform.Code, pl.ExternalUrl))]);
    }

    private async Task<EntityResolution> ResolveTrackAsync(
        StreamingSearchResult result,
        Guid? existingTrackId,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        CancellationToken cancellationToken)
    {
        if (existingTrackId is { } trackId)
        {
            var title = await dbContext.Tracks.Where(t => t.Id == trackId).Select(t => t.Title).FirstAsync(cancellationToken);
            var providerLinks = await FetchProviderLinksAsync(trackId: trackId, cancellationToken: cancellationToken);
            var artistName = await GetMainArtistNameAsync(trackId: trackId, cancellationToken: cancellationToken);
            return new EntityResolution(trackId, title, artistName, providerLinks);
        }

        var artist = new Artist { Name = result.ArtistName };
        var track = new Track { Title = result.Name };
        dbContext.AddRange(artist, track, new ArtistCredit { Artist = artist, Track = track, Role = CreditRole.MainArtist });
        var newLinks = CreateProviderLinks(result.Links, platformsByCode, track: track);
        dbContext.ProviderLinks.AddRange(newLinks);
        return new EntityResolution(track.Id, track.Title, artist.Name, newLinks);
    }

    private async Task<EntityResolution> ResolveReleaseAsync(
        StreamingSearchResult result,
        Guid? existingReleaseId,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        CancellationToken cancellationToken)
    {
        if (existingReleaseId is { } releaseId)
        {
            var title = await dbContext.Releases.Where(r => r.Id == releaseId).Select(r => r.Title).FirstAsync(cancellationToken);
            var providerLinks = await FetchProviderLinksAsync(releaseId: releaseId, cancellationToken: cancellationToken);
            var artistName = await GetMainArtistNameAsync(releaseId: releaseId, cancellationToken: cancellationToken);
            return new EntityResolution(releaseId, title, artistName, providerLinks);
        }

        var artist = new Artist { Name = result.ArtistName };
        // YouTube playlist search results carry no EP/Single/Compilation/Live signal — Album is the
        // least-wrong default for "some kind of release", not a considered classification.
        var release = new Release { Title = result.Name, Type = ReleaseType.Album };
        dbContext.AddRange(artist, release, new ArtistCredit { Artist = artist, Release = release, Role = CreditRole.MainArtist });
        var newLinks = CreateProviderLinks(result.Links, platformsByCode, release: release);
        dbContext.ProviderLinks.AddRange(newLinks);
        return new EntityResolution(release.Id, release.Title, artist.Name, newLinks);
    }

    private async Task<List<ProviderLink>> FetchProviderLinksAsync(Guid? trackId = null, Guid? releaseId = null, CancellationToken cancellationToken = default)
    {
        // Ordered client-side, not via SQL ORDER BY: SQLite's EF Core provider doesn't support ordering
        // by DateTimeOffset, and this list is small enough per catalog entity that it costs nothing.
        var links = await dbContext.ProviderLinks.Include(pl => pl.Platform)
            .Where(pl => pl.TrackId == trackId && pl.ReleaseId == releaseId)
            .ToListAsync(cancellationToken);
        return [.. links.OrderBy(pl => pl.CreatedAtUtc)];
    }

    private async Task<string> GetMainArtistNameAsync(Guid? trackId = null, Guid? releaseId = null, CancellationToken cancellationToken = default) =>
        (await dbContext.ArtistCredits.Include(ac => ac.Artist)
            .FirstAsync(ac => ac.Role == CreditRole.MainArtist && ac.TrackId == trackId && ac.ReleaseId == releaseId, cancellationToken)).Artist.Name;

    private static List<ProviderLink> CreateProviderLinks(
        IReadOnlyList<ProviderLinkCandidate> links,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        Track? track = null,
        Release? release = null) =>
        [.. links.Select(link => new ProviderLink
        {
            Track = track,
            Release = release,
            Platform = GetPlatform(platformsByCode, link.PlatformCode),
            ExternalId = link.ExternalId,
            ExternalUrl = link.ExternalUrl,
        })];

    private async Task<ProviderLink?> FindExistingProviderLinkAsync(
        IReadOnlyList<ProviderLinkCandidate> links,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        CancellationToken cancellationToken)
    {
        var externalIds = links.Select(l => l.ExternalId).Distinct().ToList();
        var candidates = await dbContext.ProviderLinks
            .Where(pl => externalIds.Contains(pl.ExternalId))
            .ToListAsync(cancellationToken);

        return links
            .Select(link => candidates.FirstOrDefault(pl => pl.ExternalId == link.ExternalId && pl.PlatformId == GetPlatform(platformsByCode, link.PlatformCode).Id))
            .FirstOrDefault(match => match is not null);
    }

    private static Platform GetPlatform(IReadOnlyDictionary<string, Platform> platformsByCode, string platformCode) =>
        platformsByCode.TryGetValue(platformCode, out var platform)
            ? platform
            : throw new InvalidOperationException($"Platform code '{platformCode}' has no seeded Platform row.");

    private readonly record struct EntityResolution(Guid EntityId, string Name, string ArtistName, List<ProviderLink> ProviderLinks);
}
