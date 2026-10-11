using Hodnota.Application.Catalog;

namespace Hodnota.Infrastructure.Providers.YouTube;

// Disambiguates between several "Artist - Topic" channels that share a name (research doc RC8/E10):
// sampling a candidate's own uploaded track titles against the catalog's already-known track names
// is the validated method (ADR 0018 D2) — label/distributor alone is not sufficient (the Kirk
// Hammett/Blackened Recordings counterexample in E10).
public sealed class YouTubeChannelResolver(IYouTubeApiClient apiClient)
{
    private const int MaxCandidates = 10;
    private const int SampleSize = 5;

    public async Task<string?> ResolveTopicChannelAsync(string artistName, IReadOnlyList<string> knownTrackTitles, CancellationToken cancellationToken)
    {
        var knownKeys = knownTrackTitles.Select(SearchResultKey.Normalize).Where(key => key.Length > 0).ToHashSet();
        if (knownKeys.Count == 0)
        {
            // Nothing to verify a candidate channel against, so no channel can be confirmed —
            // "no link is better than a pirate link."
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
            // A substring check, not exact equality: an uploaded title is often a variant like "Rude
            // Awakening (Stripped)" (E10's own sample), and "stripped"/"live"/"remix" etc. are
            // deliberately not treated as noise text elsewhere (SearchResultKey) since they denote a
            // genuinely different recording — that distinction doesn't matter for this overlap check.
            return items
                .Select(item => SearchResultKey.Normalize(item.Snippet?.Title))
                .Where(title => title.Length > 0)
                .Any(title => knownKeys.Any(known => title.Contains(known, StringComparison.Ordinal) || known.Contains(title, StringComparison.Ordinal)));
        }
        catch (StreamingProviderException)
        {
            // A candidate's uploads playlist can 404 (seen in the ADR 0018 research) — skip this one
            // candidate, not the whole resolution.
            return false;
        }
    }
}
