using Google.Apis.YouTube.v3.Data;

namespace Hodnota.Infrastructure.Providers.YouTube;

public interface IYouTubeApiClient
{
    Task<IReadOnlyList<SearchResult>> SearchVideosAsync(string query, string? channelId, int? categoryId, int maxResults, CancellationToken cancellationToken);

    Task<IReadOnlyList<SearchResult>> SearchPlaylistsAsync(string query, int maxResults, CancellationToken cancellationToken);

    Task<IReadOnlyList<SearchResult>> SearchChannelsAsync(string query, int maxResults, CancellationToken cancellationToken);

    Task<IReadOnlyList<Video>> GetVideosAsync(IReadOnlyList<string> videoIds, CancellationToken cancellationToken);

    Task<string?> GetUploadsPlaylistIdAsync(string channelId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlaylistItem>> GetPlaylistItemsAsync(string playlistId, int maxResults, CancellationToken cancellationToken);
}
