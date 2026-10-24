using Hodnota.Application.Catalog;

using Microsoft.Extensions.Caching.Memory;

namespace Hodnota.Infrastructure.Providers.YouTube;

public sealed class YouTubeChannelResolver(IYouTubeApiClient apiClient, IMemoryCache cache)
{
    private const int MaxCandidates = 25;
    private const int SampleSize = 5;

    // A found channel rarely changes, and re-finding it costs up to ~50 quota units per resolve
    // (MaxCandidates search + per-candidate uploads-playlist lookups) — caching it avoids exhausting
    // the 10,000-unit/day quota from a handful of distinct artists. A miss (null) is not cached: it
    // can be a false negative from an unlucky knownTrackTitles sample, and should be free to retry.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public async Task<string?> ResolveTopicChannelAsync(string artistName, IReadOnlyList<string> knownTrackTitles, CancellationToken cancellationToken)
    {
        var cacheKey = $"{nameof(YouTubeChannelResolver)}:{artistName.Trim().ToLowerInvariant()}";
        if (cache.TryGetValue(cacheKey, out string? cachedChannelId))
        {
            return cachedChannelId;
        }

        var knownKeys = knownTrackTitles.Select(YouTubeTrackTitleMatcher.Key).Where(key => key.Length > 0).ToHashSet();
        if (knownKeys.Count == 0)
        {
            return null;
        }

        var candidates = await apiClient.SearchChannelsAsync($"{artistName} - Topic", MaxCandidates, cancellationToken);
        foreach (var candidate in candidates)
        {
            var channelId = candidate.Id?.ChannelId;
            if (channelId is null || !YouTubeAuthenticity.IsTopicChannelTitle(candidate.Snippet?.ChannelTitle))
            {
                continue;
            }

            if (await OverlapsKnownTracksAsync(channelId, knownKeys, cancellationToken))
            {
                cache.Set(cacheKey, channelId, CacheTtl);
                return channelId;
            }
        }

        return null;
    }

    private async Task<bool> OverlapsKnownTracksAsync(string channelId, IReadOnlySet<string> knownKeys, CancellationToken cancellationToken)
    {
        try
        {
            var uploadsPlaylistId = await apiClient.GetUploadsPlaylistIdAsync(channelId, cancellationToken);
            if (uploadsPlaylistId is null)
            {
                return false;
            }

            var items = await apiClient.GetPlaylistItemsAsync(uploadsPlaylistId, SampleSize, cancellationToken);
            return items.Any(item => knownKeys.Contains(YouTubeTrackTitleMatcher.Key(item.Snippet?.Title)));
        }
        catch (StreamingProviderException ex) when (!YouTubeApiClient.IsQuotaExceeded(ex))
        {
            // A candidate's uploads playlist can 404; skip just that candidate.
            return false;
        }
    }
}
