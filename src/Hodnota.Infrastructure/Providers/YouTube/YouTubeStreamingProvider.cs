using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;

using Microsoft.AspNetCore.WebUtilities;

namespace Hodnota.Infrastructure.Providers.YouTube;

public sealed class YouTubeStreamingProvider(YouTubeService youTubeService) : IStreamingProvider
{
    private const string YouTubeHost = "https://www.youtube.com";
    private const string YouTubeMusicHost = "https://music.youtube.com";

    public async Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var request = youTubeService.Search.List("snippet");
        request.Q = query;
        request.Type = "video,playlist";
        request.MaxResults = 10;

        var response = await request.ExecuteAsync(cancellationToken);

        return [.. response.Items
            .Where(item => item.Id.VideoId is not null || item.Id.PlaylistId is not null)
            .Select(ToSearchResult)];
    }

    internal static StreamingSearchResult ToSearchResult(SearchResult item)
    {
        var isTrack = item.Id.VideoId is not null;
        var externalId = (isTrack ? item.Id.VideoId : item.Id.PlaylistId)!;
        var imageUrl = item.Snippet.Thumbnails.Medium?.Url ?? item.Snippet.Thumbnails.Default__?.Url;

        return new StreamingSearchResult(
            isTrack ? StreamingResultType.Track : StreamingResultType.Release,
            item.Snippet.Title,
            item.Snippet.ChannelTitle,
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
