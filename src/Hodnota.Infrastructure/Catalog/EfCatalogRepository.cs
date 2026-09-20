using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hodnota.Infrastructure.Catalog;

public sealed class EfCatalogRepository(ApplicationDbContext dbContext) : ICatalogRepository
{
    private const string ProviderLinkExternalIdIndexName = "IX_ProviderLinks_PlatformId_ExternalId";
    private const string TrackIsrcIndexName = "IX_Tracks_Isrc";
    private const string ReleaseUpcIndexName = "IX_Releases_Upc";

    public async Task<SharePageResult> CreateSharePageAsync(StreamingSearchResult result, CancellationToken cancellationToken)
    {
        // Each conflict type is only retried on the first two attempts, so a retry that hits the
        // *other* conflict type (e.g. the ProviderLink retry then collides on Isrc/Upc, or vice
        // versa) is still resolved instead of crashing, while a persistent, unrelated failure still
        // surfaces after two retries rather than looping forever.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await CreateSharePageCoreAsync(result, cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < 2 && IsProviderLinkExternalIdConflict(ex))
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (attempt < 2 && IsNaturalKeyConflict(ex))
            {
                // A different, unlinked provider result already created a Track/Release with this same
                // Isrc/Upc. Retrying without the natural key avoids a crash; it does not merge into
                // that existing entity.
                dbContext.ChangeTracker.Clear();
                result = result with { Isrc = null, Upc = null };
            }
        }
    }

    internal static bool IsProviderLinkExternalIdConflict(DbUpdateException ex) => ex.InnerException switch
    {
        PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == ProviderLinkExternalIdIndexName,
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode == 2067 && sqlite.Message.Contains("ProviderLinks.PlatformId, ProviderLinks.ExternalId"),
        _ => false,
    };

    internal static bool IsNaturalKeyConflict(DbUpdateException ex) => ex.InnerException switch
    {
        PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation && (pg.ConstraintName == TrackIsrcIndexName || pg.ConstraintName == ReleaseUpcIndexName),
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode == 2067 && (sqlite.Message.Contains("Tracks.Isrc") || sqlite.Message.Contains("Releases.Upc")),
        _ => false,
    };

    private async Task<SharePageResult> CreateSharePageCoreAsync(StreamingSearchResult result, CancellationToken cancellationToken)
    {
        var platformsByCode = await dbContext.Platforms
            .Where(p => result.Links.Select(l => l.PlatformCode).Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, cancellationToken);

        var existingLinksByKey = await LoadExistingLinksAsync(result.Links, platformsByCode, cancellationToken);
        var existingLink = FindExistingProviderLink(result.Links, platformsByCode, existingLinksByKey);

        var resolution = result.Type == StreamingResultType.Track
            ? await ResolveTrackAsync(result, existingLink?.TrackId, platformsByCode, existingLinksByKey, cancellationToken)
            : await ResolveReleaseAsync(result, existingLink?.ReleaseId, platformsByCode, existingLinksByKey, cancellationToken);

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
            [.. resolution.ProviderLinks.Select(pl => new SharePageLinkResult(pl.Platform.Code, pl.ExternalUrl, pl.Platform.Type))]);
    }

    public async Task<SharePageResult?> GetSharePageAsync(Guid id, CancellationToken cancellationToken)
    {
        var sharePage = await dbContext.SharePages
            .Include(sp => sp.Links.Where(l => l.IsVisible).OrderBy(l => l.DisplayOrder))
            .ThenInclude(l => l.ProviderLink)
            .ThenInclude(pl => pl.Platform)
            .Include(sp => sp.Track!.Credits.Where(c => c.Role == CreditRole.MainArtist))
            .ThenInclude(c => c.Artist)
            .Include(sp => sp.Release!.Credits.Where(c => c.Role == CreditRole.MainArtist))
            .ThenInclude(c => c.Artist)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);

        if (sharePage is null)
        {
            return null;
        }

        var type = sharePage.TrackId is not null ? StreamingResultType.Track : StreamingResultType.Release;
        var name = sharePage.Track?.Title ?? sharePage.Release!.Title;
        var artistName = (sharePage.Track?.Credits ?? sharePage.Release!.Credits)
            .First(c => c.Role == CreditRole.MainArtist).Artist.Name;

        return new SharePageResult(
            sharePage.Id,
            type,
            name,
            artistName,
            [.. sharePage.Links.Select(l => new SharePageLinkResult(l.ProviderLink.Platform.Code, l.ProviderLink.ExternalUrl, l.ProviderLink.Platform.Type))]);
    }

    private async Task<EntityResolution> ResolveTrackAsync(
        StreamingSearchResult result,
        Guid? existingTrackId,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        IReadOnlyDictionary<(Guid PlatformId, string ExternalId), ProviderLink> existingLinksByKey,
        CancellationToken cancellationToken)
    {
        if (existingTrackId is { } trackId)
        {
            var title = await dbContext.Tracks.Where(t => t.Id == trackId).Select(t => t.Title).FirstAsync(cancellationToken);
            var existingProviderLinks = await FetchProviderLinksAsync(trackId: trackId, cancellationToken: cancellationToken);
            var artistName = await GetMainArtistNameAsync(trackId: trackId, cancellationToken: cancellationToken);

            var newLinks = CreateMissingProviderLinks(result.Links, platformsByCode, existingLinksByKey, trackId: trackId);
            dbContext.ProviderLinks.AddRange(newLinks);

            return new EntityResolution(trackId, title, artistName, [.. existingProviderLinks, .. newLinks]);
        }

        var artist = new Artist { Name = result.ArtistName };
        var track = new Track { Title = result.Name, Isrc = result.Isrc };
        dbContext.AddRange(artist, track, new ArtistCredit { Artist = artist, Track = track, Role = CreditRole.MainArtist });
        var links = CreateMissingProviderLinks(result.Links, platformsByCode, existingLinksByKey, track: track);
        dbContext.ProviderLinks.AddRange(links);
        return new EntityResolution(track.Id, track.Title, artist.Name, links);
    }

    private async Task<EntityResolution> ResolveReleaseAsync(
        StreamingSearchResult result,
        Guid? existingReleaseId,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        IReadOnlyDictionary<(Guid PlatformId, string ExternalId), ProviderLink> existingLinksByKey,
        CancellationToken cancellationToken)
    {
        if (existingReleaseId is { } releaseId)
        {
            var title = await dbContext.Releases.Where(r => r.Id == releaseId).Select(r => r.Title).FirstAsync(cancellationToken);
            var existingProviderLinks = await FetchProviderLinksAsync(releaseId: releaseId, cancellationToken: cancellationToken);
            var artistName = await GetMainArtistNameAsync(releaseId: releaseId, cancellationToken: cancellationToken);

            var newLinks = CreateMissingProviderLinks(result.Links, platformsByCode, existingLinksByKey, releaseId: releaseId);
            dbContext.ProviderLinks.AddRange(newLinks);

            return new EntityResolution(releaseId, title, artistName, [.. existingProviderLinks, .. newLinks]);
        }

        var artist = new Artist { Name = result.ArtistName };
        // Least-wrong default for a provider that carries no release-type signal (e.g. a YouTube
        // playlist). Spotify's album_type maps onto ReleaseType directly — see SpotifyStreamingProvider.
        var release = new Release { Title = result.Name, Type = result.ReleaseType ?? ReleaseType.Album, Upc = result.Upc };
        dbContext.AddRange(artist, release, new ArtistCredit { Artist = artist, Release = release, Role = CreditRole.MainArtist });
        var links = CreateMissingProviderLinks(result.Links, platformsByCode, existingLinksByKey, release: release);
        dbContext.ProviderLinks.AddRange(links);
        return new EntityResolution(release.Id, release.Title, artist.Name, links);
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

    private static List<ProviderLink> CreateMissingProviderLinks(
        IReadOnlyList<ProviderLinkCandidate> links,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        IReadOnlyDictionary<(Guid PlatformId, string ExternalId), ProviderLink> existingLinksByKey,
        Guid? trackId = null,
        Guid? releaseId = null,
        Track? track = null,
        Release? release = null)
    {
        var newLinks = new List<ProviderLink>();
        foreach (var link in links)
        {
            var platform = GetPlatform(platformsByCode, link.PlatformCode);
            if (existingLinksByKey.ContainsKey((platform.Id, link.ExternalId)))
            {
                continue;
            }

            newLinks.Add(new ProviderLink
            {
                TrackId = trackId,
                ReleaseId = releaseId,
                Track = track,
                Release = release,
                Platform = platform,
                ExternalId = link.ExternalId,
                ExternalUrl = link.ExternalUrl,
            });
        }

        return newLinks;
    }

    private static ProviderLink? FindExistingProviderLink(
        IReadOnlyList<ProviderLinkCandidate> links,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        IReadOnlyDictionary<(Guid PlatformId, string ExternalId), ProviderLink> existingLinksByKey) =>
        links
            .Select(link => existingLinksByKey.GetValueOrDefault((GetPlatform(platformsByCode, link.PlatformCode).Id, link.ExternalId)))
            .FirstOrDefault(match => match is not null);

    private async Task<Dictionary<(Guid PlatformId, string ExternalId), ProviderLink>> LoadExistingLinksAsync(
        IReadOnlyList<ProviderLinkCandidate> links,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        CancellationToken cancellationToken)
    {
        var externalIds = links.Select(l => l.ExternalId).Distinct().ToList();
        var candidates = await dbContext.ProviderLinks
            .Where(pl => externalIds.Contains(pl.ExternalId))
            .ToListAsync(cancellationToken);

        var existingLinksByKey = new Dictionary<(Guid, string), ProviderLink>();
        foreach (var link in links)
        {
            var platformId = GetPlatform(platformsByCode, link.PlatformCode).Id;
            var match = candidates.FirstOrDefault(pl => pl.PlatformId == platformId && pl.ExternalId == link.ExternalId);
            if (match is not null)
            {
                existingLinksByKey[(platformId, link.ExternalId)] = match;
            }
        }

        return existingLinksByKey;
    }

    private static Platform GetPlatform(IReadOnlyDictionary<string, Platform> platformsByCode, string platformCode) =>
        platformsByCode.TryGetValue(platformCode, out var platform)
            ? platform
            : throw new InvalidOperationException($"Platform code '{platformCode}' has no seeded Platform row.");

    private readonly record struct EntityResolution(Guid EntityId, string Name, string ArtistName, List<ProviderLink> ProviderLinks);
}
