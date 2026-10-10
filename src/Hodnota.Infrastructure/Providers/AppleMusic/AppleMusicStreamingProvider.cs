using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.AspNetCore.WebUtilities;

namespace Hodnota.Infrastructure.Providers.AppleMusic;

public sealed class AppleMusicStreamingProvider(AppleMusicApiClient apiClient, AppleMusicSettings settings) : IStreamingProvider, IStreamingNameLookup
{
    private const string UnknownArtist = "Unknown";

    private const string SingleSuffix = " - Single";

    private const string EpSuffix = " - EP";

    private const int MaxAlbumResults = 5;

    public string ProviderCode => ProviderCodes.AppleMusic;

    public IReadOnlyList<string> LinkPlatformCodes { get; } = [PlatformCodes.AppleMusic];

    public bool Supports(StreamingResultType type) => true;

    // The UPC lookup is too fuzzy to trust (ADR 0016: a fabricated barcode still returned 24 unrelated
    // albums, and no result reports its own UPC to check against), and there is no ISRC lookup at all.
    public bool SupportsLookup(StreamingResultType type) => false;

    public Task<IReadOnlyList<StreamingSearchResult>> LookupAsync(StreamingLookupKey key, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public async Task<IReadOnlyList<StreamingSearchResult>> FindByNameAsync(string artistName, string name, StreamingResultType type, CancellationToken cancellationToken)
    {
        var results = await SearchAsync($"{artistName} {name}", type, cancellationToken);
        return [.. results.Where(result => result.Type == type && SearchResultNameMatcher.IsSameItem(result, artistName, name))];
    }

    public async Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        if (type == StreamingResultType.Track)
        {
            var songs = await apiClient.SearchSongsAsync(query, cancellationToken);
            return [.. songs.Where(IsUsableTrack).Select(item => ToTrackResult(item, settings.Country))];
        }

        // The album-search endpoint misses real albums (ADR 0016), so results from it are combined
        // with albums grouped out of a song search, album-search hits taking priority on a tie.
        var albumTask = apiClient.SearchAlbumsAsync(query, cancellationToken);
        var songTask = apiClient.SearchSongsForAlbumsAsync(query, cancellationToken);
        await Task.WhenAll(albumTask, songTask);
        var albumItems = albumTask.Result;
        var songItems = songTask.Result;

        var seenAlbumIds = new HashSet<long>();
        var merged = new List<ITunesItem>();
        foreach (var item in albumItems.Where(IsUsableAlbum).Where(item => seenAlbumIds.Add(item.CollectionId!.Value)))
        {
            merged.Add(item);
        }

        foreach (var item in songItems.Where(IsUsableAlbum).Where(item => seenAlbumIds.Add(item.CollectionId!.Value)))
        {
            merged.Add(item);
        }

        var queryWords = SearchResultRelevanceFilter.ParseQueryWords(query);
        var candidates = merged
            .Select(item => ToAlbumResult(item, settings.Country))
            .Where(result => SearchResultRelevanceFilter.IsRelevant(queryWords, result));

        // Confirmed live (ADR 0016): entity=album alone can return a full page of other albums by
        // the same artist (which also pass the relevance check, since the artist name is one of the
        // query words), filling the whole cap before the song-grouped supplement is even considered
        // — defeating the reason that supplement exists. A result whose own name (not just the
        // shared artist) matches the query is kept ahead of one that matches only on artist.
        return [.. candidates
            .OrderByDescending(result => SearchResultRelevanceFilter.IsRelevant(queryWords, result with { ArtistName = string.Empty }))
            .Take(MaxAlbumResults)];
    }

    internal static bool IsUsableTrack(ITunesItem item) => item.TrackId.HasValue && !string.IsNullOrWhiteSpace(item.TrackName);

    internal static bool IsUsableAlbum(ITunesItem item) => item.CollectionId.HasValue && !string.IsNullOrWhiteSpace(item.CollectionName);

    internal static StreamingSearchResult ToTrackResult(ITunesItem item, string country)
    {
        var externalId = item.TrackId!.Value.ToString();

        return new StreamingSearchResult(
            StreamingResultType.Track,
            item.TrackName!,
            item.ArtistName is { Length: > 0 } artist ? artist : UnknownArtist,
            PickImage(item.ArtworkUrl100),
            [new ProviderLinkCandidate(PlatformCodes.AppleMusic, externalId, BuildTrackUrl(item.TrackViewUrl, country, externalId))]);
    }

    internal static StreamingSearchResult ToAlbumResult(ITunesItem item, string country)
    {
        var externalId = item.CollectionId!.Value.ToString();
        var (name, releaseType) = ParseAlbumName(item.CollectionName!);

        return new StreamingSearchResult(
            StreamingResultType.Release,
            name,
            item.ArtistName is { Length: > 0 } artist ? artist : UnknownArtist,
            PickImage(item.ArtworkUrl100),
            [new ProviderLinkCandidate(PlatformCodes.AppleMusic, externalId, BuildAlbumUrl(item.CollectionViewUrl, country, externalId))],
            releaseType);
    }

    // iTunes appends " - Single"/" - EP" to the title rather than exposing a separate field (confirmed
    // live, e.g. "Kafka - Single", "Micro - EP"). No UPC is returned by this endpoint (ADR 0016).
    internal static (string Name, ReleaseType ReleaseType) ParseAlbumName(string collectionName)
    {
        if (collectionName.EndsWith(SingleSuffix, StringComparison.Ordinal))
        {
            return (collectionName[..^SingleSuffix.Length], ReleaseType.Single);
        }

        if (collectionName.EndsWith(EpSuffix, StringComparison.Ordinal))
        {
            return (collectionName[..^EpSuffix.Length], ReleaseType.EP);
        }

        return (collectionName, ReleaseType.Album);
    }

    // 100x100bb -> 600x600bb, confirmed live to resolve to a real image; the original URL is kept
    // unchanged if that size marker isn't present.
    internal static Uri? PickImage(string? artworkUrl100)
    {
        if (string.IsNullOrEmpty(artworkUrl100))
        {
            return null;
        }

        var upsized = artworkUrl100.Replace("100x100bb", "600x600bb", StringComparison.Ordinal);
        return Uri.TryCreate(upsized, UriKind.Absolute, out var uri) ? uri : null;
    }

    internal static Uri BuildTrackUrl(string? trackViewUrl, string country, string externalId)
    {
        if (!TryCreateHttpUri(trackViewUrl, out var uri))
        {
            return new Uri($"https://music.apple.com/{country}/song/{externalId}");
        }

        // Keep the "i" (track) query parameter, drop only Apple's "uo" referral-tracking parameter.
        var kept = QueryHelpers.ParseQuery(uri.Query)
            .Where(pair => pair.Key != "uo")
            .ToDictionary(pair => pair.Key, pair => (string?)pair.Value.ToString());

        var withoutQuery = new UriBuilder(uri) { Query = string.Empty }.Uri.ToString();
        return new Uri(QueryHelpers.AddQueryString(withoutQuery, kept));
    }

    internal static Uri BuildAlbumUrl(string? collectionViewUrl, string country, string externalId)
    {
        if (!TryCreateHttpUri(collectionViewUrl, out var uri))
        {
            return new Uri($"https://music.apple.com/{country}/album/{externalId}");
        }

        // Drop the whole query: a song-derived collectionViewUrl carries "?i={trackId}", which would
        // point the album link at one track instead of the album.
        return new UriBuilder(uri) { Query = string.Empty }.Uri;
    }

    private static bool TryCreateHttpUri(string? value, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri!) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
