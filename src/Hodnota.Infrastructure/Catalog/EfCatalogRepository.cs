using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hodnota.Infrastructure.Catalog;

public sealed class EfCatalogRepository(ApplicationDbContext dbContext, TimeProvider? timeProvider = null) : ICatalogRepository
{
    private const string ProviderLinkExternalIdIndexName = "IX_ProviderLinks_PlatformId_ExternalId";
    private const string TrackIsrcIndexName = "IX_Tracks_Isrc";
    private const string ReleaseUpcIndexName = "IX_Releases_Upc";
    private const double ExactMatchConfidence = 1.0;
    private const double NameMatchConfidence = 0.5;

    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public async Task<SharePageResult> ResolveSharePageAsync(StreamingSearchResult result, CancellationToken cancellationToken)
    {
        // A concurrent resolve of the same item saved first and took the provider link or the
        // ISRC/UPC, or two of them waited on each other's unique keys and Postgres ended one as the
        // deadlock victim. The retry finds that item and its share page instead of creating them
        // again. A persistent, unrelated failure still surfaces after two retries rather than looping.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await ResolveSharePageCoreAsync(result, cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < 2 && (IsProviderLinkExternalIdConflict(ex) || IsNaturalKeyConflict(ex) || IsDeadlock(ex)))
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    internal static bool IsDeadlock(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected };

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

    private async Task<SharePageResult> ResolveSharePageCoreAsync(StreamingSearchResult result, CancellationToken cancellationToken)
    {
        var upcVariants = CatalogKeys.BarcodeVariants(result.Upc);
        result = result with { Isrc = CatalogKeys.NormalizeIsrc(result.Isrc), Upc = CatalogKeys.NormalizeBarcode(result.Upc) };

        var platformsByCode = await dbContext.Platforms
            .Where(p => result.Links.Select(l => l.PlatformCode).Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, cancellationToken);

        var existingLinksByKey = await LoadExistingLinksAsync(result.Links, platformsByCode, cancellationToken);
        var existingLink = FindExistingProviderLink(result.Links, platformsByCode, existingLinksByKey);

        var resolution = result.Type == StreamingResultType.Track
            ? await ResolveTrackAsync(result, existingLink?.TrackId ?? await FindTrackIdByIsrcAsync(result.Isrc, cancellationToken), platformsByCode, existingLinksByKey, cancellationToken)
            : await ResolveReleaseAsync(result, existingLink?.ReleaseId ?? await FindReleaseIdByUpcAsync(upcVariants, cancellationToken), platformsByCode, existingLinksByKey, cancellationToken);

        var sharePage = await FindOrCreateSharePageAsync(result.Type, resolution, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Cleared so the filtered Include below reads the saved state, not tracked hidden links.
        dbContext.ChangeTracker.Clear();
        return (await GetSharePageAsync(sharePage.Id, cancellationToken))!;
    }

    public async Task<SharePageResult?> GetSharePageAsync(Guid id, CancellationToken cancellationToken)
    {
        var sharePage = await dbContext.SharePages
            .AsNoTracking()
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

    public Task<bool> SharePageExistsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SharePages.AsNoTracking().AnyAsync(sp => sp.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PlatformCheckResult>> GetPlatformChecksAsync(Guid sharePageId, CancellationToken cancellationToken)
    {
        var target = await FindTargetAsync(sharePageId, cancellationToken);
        if (target is null)
        {
            return [];
        }

        var (trackId, releaseId) = target.Value;
        var checks = await dbContext.ProviderChecks.AsNoTracking().Include(pc => pc.Platform)
            .Where(pc => pc.TrackId == trackId && pc.ReleaseId == releaseId)
            .ToListAsync(cancellationToken);

        return [.. checks.Select(check => new PlatformCheckResult(check.Platform.Code, check.Outcome))];
    }

    public async Task<IReadOnlyDictionary<string, PlatformType>> GetPlatformTypesAsync(IReadOnlyCollection<string> platformCodes, CancellationToken cancellationToken) =>
        await dbContext.Platforms.AsNoTracking()
            .Where(p => platformCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Type, cancellationToken);

    public async Task<EnrichmentRequest?> GetEnrichmentRequestAsync(Guid sharePageId, CancellationToken cancellationToken)
    {
        var page = await dbContext.SharePages
            .Include(sp => sp.Track)
            .Include(sp => sp.Release)
            .FirstOrDefaultAsync(sp => sp.Id == sharePageId, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var links = await FetchProviderLinksAsync(page.TrackId, page.ReleaseId, cancellationToken);
        var artistName = await GetMainArtistNameAsync(page.TrackId, page.ReleaseId, cancellationToken);

        return new EnrichmentRequest(
            page.TrackId is not null ? StreamingResultType.Track : StreamingResultType.Release,
            page.Track?.Title ?? page.Release!.Title,
            artistName,
            page.Track?.Isrc,
            page.Release?.Upc,
            [.. links.Select(link => new ProviderLinkCandidate(link.Platform.Code, link.ExternalId, link.ExternalUrl))]);
    }

    public async Task<IReadOnlyList<PlatformCheckResult>> SaveEnrichmentAsync(Guid sharePageId, ProviderEnrichment enrichment, CancellationToken cancellationToken)
    {
        var target = await FindTargetAsync(sharePageId, cancellationToken);
        if (target is null)
        {
            return [];
        }

        var (trackId, releaseId) = target.Value;
        var now = _clock.GetUtcNow();
        // A kept search-row link (Confirmed == false) is not re-verified by this check, so its
        // LastVerifiedUtc must not move — only a provider that actually answered just now sets it.
        DateTimeOffset? verifiedAt = enrichment.Confirmed ? now : null;

        var codes = enrichment.PlatformCodes.Concat(enrichment.Links.Select(l => l.PlatformCode)).Distinct().ToList();
        var platformsByCode = await dbContext.Platforms.Where(p => codes.Contains(p.Code)).ToDictionaryAsync(p => p.Code, cancellationToken);
        var entityLinks = await FetchProviderLinksAsync(trackId, releaseId, cancellationToken);
        var outcomes = enrichment.PlatformCodes.ToDictionary(code => code, _ => enrichment.Outcome);
        var newLinks = new List<ProviderLink>();

        if (enrichment.Outcome is LookupOutcome.ExactMatch or LookupOutcome.NameMatch)
        {
            var isExact = enrichment.Outcome == LookupOutcome.ExactMatch;

            // Candidates for one platform are tried in the provider's own preference order (e.g. a
            // Discogs master, then its release); the first one not already linked to a different
            // catalog entity is kept. One link per platform (unique index), so only one can win.
            foreach (var group in enrichment.Links.GroupBy(link => link.PlatformCode))
            {
                var platform = GetPlatform(platformsByCode, group.Key);
                var current = entityLinks.FirstOrDefault(link => link.PlatformId == platform.Id);
                var matched = false;

                foreach (var candidate in group)
                {
                    if (current is not null && current.ExternalId == candidate.ExternalId)
                    {
                        current.Confidence = isExact ? ExactMatchConfidence : current.Confidence ?? NameMatchConfidence;
                        if (verifiedAt is not null)
                        {
                            current.LastVerifiedUtc = verifiedAt;
                        }

                        matched = true;
                        break;
                    }

                    if (current is null)
                    {
                        if (await IsProviderLinkTakenAsync(platform.Id, candidate.ExternalId, cancellationToken))
                        {
                            continue;
                        }

                        current = new ProviderLink
                        {
                            TrackId = trackId,
                            ReleaseId = releaseId,
                            Platform = platform,
                            ExternalId = candidate.ExternalId,
                            ExternalUrl = candidate.ExternalUrl,
                            Confidence = isExact ? ExactMatchConfidence : NameMatchConfidence,
                            LastVerifiedUtc = verifiedAt,
                        };
                        newLinks.Add(current);
                        matched = true;
                        break;
                    }

                    if (!isExact)
                    {
                        // A name match never overwrites a different existing link — only a fresh
                        // exact match can; try the next candidate for one that matches current instead.
                        continue;
                    }

                    if (await IsProviderLinkTakenAsync(platform.Id, candidate.ExternalId, cancellationToken))
                    {
                        continue;
                    }

                    current.ExternalId = candidate.ExternalId;
                    current.ExternalUrl = candidate.ExternalUrl;
                    current.Confidence = ExactMatchConfidence;
                    if (verifiedAt is not null)
                    {
                        current.LastVerifiedUtc = verifiedAt;
                    }

                    matched = true;
                    break;
                }

                // Not matched: every candidate was either someone else's link (isExact) or not worth
                // overwriting current with (!isExact) — the entity keeps whatever it already had.
                outcomes[platform.Code] = matched ? enrichment.Outcome
                    : current is not null ? LookupOutcome.NameMatch
                    : LookupOutcome.NotFound;
            }
        }

        dbContext.ProviderLinks.AddRange(newLinks);
        if (newLinks.Count > 0)
        {
            var pages = await dbContext.SharePages.Include(sp => sp.Links)
                .Where(sp => sp.TrackId == trackId && sp.ReleaseId == releaseId)
                .ToListAsync(cancellationToken);
            foreach (var page in pages)
            {
                var order = page.Links.Count == 0 ? 0 : page.Links.Max(l => l.DisplayOrder) + 1;
                foreach (var link in newLinks)
                {
                    dbContext.SharePageLinks.Add(new SharePageLink { SharePage = page, ProviderLink = link, DisplayOrder = order++ });
                }
            }
        }

        var existingChecks = await dbContext.ProviderChecks
            .Where(pc => pc.TrackId == trackId && pc.ReleaseId == releaseId)
            .ToListAsync(cancellationToken);
        foreach (var (code, outcome) in outcomes)
        {
            var platform = GetPlatform(platformsByCode, code);
            var check = existingChecks.FirstOrDefault(pc => pc.PlatformId == platform.Id);
            if (check is null)
            {
                dbContext.ProviderChecks.Add(new ProviderCheck { TrackId = trackId, ReleaseId = releaseId, Platform = platform, Outcome = outcome, CheckedAtUtc = now });
            }
            else
            {
                check.Outcome = outcome;
                check.CheckedAtUtc = now;
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another writer got there first. Dropping the failed changes lets the caller's next
            // save on this context start clean.
            dbContext.ChangeTracker.Clear();
            throw;
        }

        return [.. outcomes.Select(pair => new PlatformCheckResult(pair.Key, pair.Value))];
    }

    private async Task<(Guid? TrackId, Guid? ReleaseId)?> FindTargetAsync(Guid sharePageId, CancellationToken cancellationToken)
    {
        var page = await dbContext.SharePages.AsNoTracking()
            .Where(sp => sp.Id == sharePageId)
            .Select(sp => new { sp.TrackId, sp.ReleaseId })
            .FirstOrDefaultAsync(cancellationToken);
        return page is null ? null : (page.TrackId, page.ReleaseId);
    }

    private Task<bool> IsProviderLinkTakenAsync(Guid platformId, string externalId, CancellationToken cancellationToken) =>
        dbContext.ProviderLinks.AnyAsync(pl => pl.PlatformId == platformId && pl.ExternalId == externalId, cancellationToken);

    private async Task<Guid?> FindTrackIdByIsrcAsync(string? isrc, CancellationToken cancellationToken) =>
        isrc is null
            ? null
            : await dbContext.Tracks.Where(t => t.Isrc == isrc).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(cancellationToken);

    private async Task<Guid?> FindReleaseIdByUpcAsync(IReadOnlyList<string> upcVariants, CancellationToken cancellationToken)
    {
        if (upcVariants.Count == 0)
        {
            return null;
        }

        var codes = upcVariants.ToArray();
        return await dbContext.Releases.Where(r => r.Upc != null && codes.Contains(r.Upc)).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<SharePage> FindOrCreateSharePageAsync(StreamingResultType type, EntityResolution resolution, CancellationToken cancellationToken)
    {
        Guid? trackId = type == StreamingResultType.Track ? resolution.EntityId : null;
        Guid? releaseId = type == StreamingResultType.Release ? resolution.EntityId : null;

        // Oldest first, client-side: SQLite's EF Core provider can't order by DateTimeOffset. An item
        // can have several pages from before one page per item was the rule.
        var pages = await dbContext.SharePages.Include(sp => sp.Links)
            .Where(sp => sp.TrackId == trackId && sp.ReleaseId == releaseId)
            .ToListAsync(cancellationToken);
        var sharePage = pages.OrderBy(sp => sp.CreatedAtUtc).FirstOrDefault();

        if (sharePage is null)
        {
            sharePage = new SharePage { TrackId = trackId, ReleaseId = releaseId };
            dbContext.SharePages.Add(sharePage);
        }

        var linkedIds = sharePage.Links.Select(l => l.ProviderLinkId).ToHashSet();
        var order = sharePage.Links.Count == 0 ? 0 : sharePage.Links.Max(l => l.DisplayOrder) + 1;
        foreach (var providerLink in resolution.ProviderLinks.Where(pl => !linkedIds.Contains(pl.Id)))
        {
            dbContext.SharePageLinks.Add(new SharePageLink { SharePage = sharePage, ProviderLink = providerLink, DisplayOrder = order++ });
        }

        return sharePage;
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
            var track = await dbContext.Tracks.FirstAsync(t => t.Id == trackId, cancellationToken);
            if (track.Isrc is null && result.Isrc is not null && !await dbContext.Tracks.AnyAsync(t => t.Isrc == result.Isrc, cancellationToken))
            {
                track.Isrc = result.Isrc;
            }

            var existingProviderLinks = await FetchProviderLinksAsync(trackId: trackId, cancellationToken: cancellationToken);
            var artistName = await GetMainArtistNameAsync(trackId: trackId, cancellationToken: cancellationToken);

            var newLinks = CreateMissingProviderLinks(result.Links, platformsByCode, existingLinksByKey, existingProviderLinks, trackId: trackId);
            dbContext.ProviderLinks.AddRange(newLinks);

            return new EntityResolution(trackId, track.Title, artistName, [.. existingProviderLinks, .. newLinks]);
        }

        var artist = new Artist { Name = result.ArtistName };
        var newTrack = new Track { Title = result.Name, Isrc = result.Isrc };
        dbContext.AddRange(artist, newTrack, new ArtistCredit { Artist = artist, Track = newTrack, Role = CreditRole.MainArtist });
        var links = CreateMissingProviderLinks(result.Links, platformsByCode, existingLinksByKey, [], track: newTrack);
        dbContext.ProviderLinks.AddRange(links);
        return new EntityResolution(newTrack.Id, newTrack.Title, artist.Name, links);
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
            var release = await dbContext.Releases.FirstAsync(r => r.Id == releaseId, cancellationToken);
            if (release.Upc is null && result.Upc is not null)
            {
                var codes = CatalogKeys.BarcodeVariants(result.Upc).ToArray();
                if (!await dbContext.Releases.AnyAsync(r => r.Upc != null && codes.Contains(r.Upc), cancellationToken))
                {
                    release.Upc = result.Upc;
                }
            }

            var existingProviderLinks = await FetchProviderLinksAsync(releaseId: releaseId, cancellationToken: cancellationToken);
            var artistName = await GetMainArtistNameAsync(releaseId: releaseId, cancellationToken: cancellationToken);

            var newLinks = CreateMissingProviderLinks(result.Links, platformsByCode, existingLinksByKey, existingProviderLinks, releaseId: releaseId);
            dbContext.ProviderLinks.AddRange(newLinks);

            return new EntityResolution(releaseId, release.Title, artistName, [.. existingProviderLinks, .. newLinks]);
        }

        var artist = new Artist { Name = result.ArtistName };
        // Least-wrong default for a provider that carries no release-type signal (e.g. a YouTube
        // playlist). Spotify's album_type maps onto ReleaseType directly — see SpotifyStreamingProvider.
        var newRelease = new Release { Title = result.Name, Type = result.ReleaseType ?? ReleaseType.Album, Upc = result.Upc };
        dbContext.AddRange(artist, newRelease, new ArtistCredit { Artist = artist, Release = newRelease, Role = CreditRole.MainArtist });
        var links = CreateMissingProviderLinks(result.Links, platformsByCode, existingLinksByKey, [], release: newRelease);
        dbContext.ProviderLinks.AddRange(links);
        return new EntityResolution(newRelease.Id, newRelease.Title, artist.Name, links);
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

    // A platform holds one link per entity (unique index), so a candidate for a platform the entity
    // already has is skipped, as is one whose platform item another entity already owns.
    private static List<ProviderLink> CreateMissingProviderLinks(
        IReadOnlyList<ProviderLinkCandidate> links,
        IReadOnlyDictionary<string, Platform> platformsByCode,
        IReadOnlyDictionary<(Guid PlatformId, string ExternalId), ProviderLink> existingLinksByKey,
        IReadOnlyCollection<ProviderLink> entityLinks,
        Guid? trackId = null,
        Guid? releaseId = null,
        Track? track = null,
        Release? release = null)
    {
        var occupiedPlatformIds = entityLinks.Select(link => link.PlatformId).ToHashSet();
        var newLinks = new List<ProviderLink>();
        foreach (var link in links)
        {
            var platform = GetPlatform(platformsByCode, link.PlatformCode);
            if (existingLinksByKey.ContainsKey((platform.Id, link.ExternalId)) || !occupiedPlatformIds.Add(platform.Id))
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
