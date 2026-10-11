using Google.Apis.YouTube.v3.Data;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.AspNetCore.WebUtilities;

namespace Hodnota.Infrastructure.Providers.YouTube;

public sealed class YouTubeStreamingProvider(IYouTubeApiClient apiClient, YouTubeChannelResolver channelResolver) : IStreamingProvider, IStreamingNameLookup
{
    private const string YouTubeHost = "https://www.youtube.com";
    private const string YouTubeMusicHost = "https://music.youtube.com";
    private const string UnknownArtist = "Unknown";
    private const int MusicCategoryId = 10;
    private const int MaxResults = 50;
    private const int MaxChannelVideoResults = 25;

    public string ProviderCode => ProviderCodes.YouTube;

    public IReadOnlyList<string> LinkPlatformCodes { get; } = [PlatformCodes.YouTube, PlatformCodes.YouTubeMusic];

    public bool Supports(StreamingResultType type) => true;

    public bool SupportsLookup(StreamingResultType type) => false;

    public Task<IReadOnlyList<StreamingSearchResult>> LookupAsync(StreamingLookupKey key, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public async Task<IReadOnlyList<StreamingSearchResult>> FindByNameAsync(string artistName, string name, StreamingResultType type, IReadOnlyList<string> knownTrackTitles, CancellationToken cancellationToken)
    {
        var results = await SearchAsync($"{artistName} {name}", type, cancellationToken);
        var matches = results.Where(result => result.Type == type && SearchResultNameMatcher.IsSameItem(result, artistName, name)).ToList();
        if (matches.Count > 0 || type != StreamingResultType.Release)
        {
            return matches;
        }

        // No verifiable OLAK album playlist — fall back to a verified Topic track of the album
        // (ADR 0018 D2), found by confirming the artist's Topic channel (E10) and searching inside
        // it for the album title (E9).
        var channelId = await channelResolver.ResolveTopicChannelAsync(artistName, knownTrackTitles, cancellationToken);
        if (channelId is null)
        {
            return [];
        }

        var channelItems = await apiClient.SearchVideosAsync(name, channelId, MusicCategoryId, MaxChannelVideoResults, cancellationToken);
        var track = channelItems.Where(IsUsable).Where(YouTubeAuthenticity.HasIntrinsicAuthenticity).Select(ToSearchResult).FirstOrDefault();
        return track is null ? [] : [track];
    }

    public async Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        var items = type == StreamingResultType.Track
            ? await apiClient.SearchVideosAsync(query, channelId: null, categoryId: null, MaxResults, cancellationToken)
            : await apiClient.SearchPlaylistsAsync(query, MaxResults, cancellationToken);

        return [.. items.Where(IsUsable).Where(YouTubeAuthenticity.HasIntrinsicAuthenticity).Select(ToSearchResult)];
    }

    internal static bool IsUsable(SearchResult item) =>
        (item.Id.VideoId is not null || item.Id.PlaylistId is not null) && !string.IsNullOrWhiteSpace(item.Snippet?.Title);

    internal static StreamingSearchResult ToSearchResult(SearchResult item)
    {
        var isTrack = item.Id.VideoId is not null;
        var externalId = (isTrack ? item.Id.VideoId : item.Id.PlaylistId)!;
        var imageUrl = item.Snippet.Thumbnails?.Medium?.Url ?? item.Snippet.Thumbnails?.Default__?.Url;
        var artistName = string.IsNullOrWhiteSpace(item.Snippet.ChannelTitle) ? UnknownArtist : item.Snippet.ChannelTitle;

        return new StreamingSearchResult(
            isTrack ? StreamingResultType.Track : StreamingResultType.Release,
            item.Snippet.Title,
            artistName,
            imageUrl is null ? null : new Uri(imageUrl),
            [
                new ProviderLinkCandidate(PlatformCodes.YouTube, externalId, BuildUrl(YouTubeHost, isTrack, externalId)),
                new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, externalId, BuildUrl(YouTubeMusicHost, isTrack, externalId)),
            ]);
    }

    private static Uri BuildUrl(string host, bool isTrack, string externalId)
    {
        var path = isTrack ? $"{host}/watch" : $"{host}/playlist";
        var queryParamName = isTrack ? "v" : "list";
        return new Uri(QueryHelpers.AddQueryString(path, queryParamName, externalId));
    }
}
