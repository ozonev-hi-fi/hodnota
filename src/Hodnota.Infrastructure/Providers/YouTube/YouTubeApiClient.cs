using Google;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

using Hodnota.Application.Catalog;

namespace Hodnota.Infrastructure.Providers.YouTube;

// Wraps the YouTubeService singleton so the provider can ask for more than one search.list call
// without rebuilding the Search.List request inline each time, and so every YouTube call (not just
// search) goes through one place that turns a failure into StreamingProviderException.
public sealed class YouTubeApiClient(YouTubeService youTubeService) : IYouTubeApiClient
{
    public Task<IReadOnlyList<SearchResult>> SearchVideosAsync(string query, string? channelId, int? categoryId, int maxResults, CancellationToken cancellationToken) =>
        SearchAsync(query, "video", channelId, categoryId, maxResults, cancellationToken);

    public Task<IReadOnlyList<SearchResult>> SearchPlaylistsAsync(string query, int maxResults, CancellationToken cancellationToken) =>
        SearchAsync(query, "playlist", channelId: null, categoryId: null, maxResults, cancellationToken);

    public Task<IReadOnlyList<SearchResult>> SearchChannelsAsync(string query, int maxResults, CancellationToken cancellationToken) =>
        SearchAsync(query, "channel", channelId: null, categoryId: null, maxResults, cancellationToken);

    public async Task<IReadOnlyList<Video>> GetVideosAsync(IReadOnlyList<string> videoIds, CancellationToken cancellationToken)
    {
        if (videoIds.Count == 0)
        {
            return [];
        }

        var request = youTubeService.Videos.List("snippet");
        request.Id = videoIds.ToArray();

        var response = await ExecuteAsync(request.ExecuteAsync(cancellationToken), "videos");
        return response.Items?.ToList() ?? [];
    }

    public async Task<string?> GetUploadsPlaylistIdAsync(string channelId, CancellationToken cancellationToken)
    {
        var request = youTubeService.Channels.List("contentDetails");
        request.Id = channelId;

        var response = await ExecuteAsync(request.ExecuteAsync(cancellationToken), "channels");
        return response.Items?.FirstOrDefault()?.ContentDetails?.RelatedPlaylists?.Uploads;
    }

    public async Task<IReadOnlyList<PlaylistItem>> GetPlaylistItemsAsync(string playlistId, int maxResults, CancellationToken cancellationToken)
    {
        var request = youTubeService.PlaylistItems.List("snippet");
        request.PlaylistId = playlistId;
        request.MaxResults = maxResults;

        var response = await ExecuteAsync(request.ExecuteAsync(cancellationToken), "playlistItems");
        return response.Items?.ToList() ?? [];
    }

    private async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, string type, string? channelId, int? categoryId, int maxResults, CancellationToken cancellationToken)
    {
        var request = youTubeService.Search.List("snippet");
        request.Q = query;
        request.Type = type;
        request.MaxResults = maxResults;
        request.ChannelId = channelId;
        request.VideoCategoryId = categoryId?.ToString();

        var response = await ExecuteAsync(request.ExecuteAsync(cancellationToken), "search");
        return response.Items?.ToList() ?? [];
    }

    private static async Task<T> ExecuteAsync<T>(Task<T> execution, string requestName)
    {
        try
        {
            return await execution;
        }
        catch (GoogleApiException ex)
        {
            throw new StreamingProviderException($"YouTube {requestName} request failed.", ex);
        }
    }
}
