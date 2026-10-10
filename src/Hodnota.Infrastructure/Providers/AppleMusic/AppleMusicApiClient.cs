using Hodnota.Application.Catalog;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.AppleMusic;

public sealed class AppleMusicApiClient(IHttpClientFactory httpClientFactory, AppleMusicSettings settings, ILogger<AppleMusicApiClient> logger)
{
    private const int TrackResultLimit = 5;

    private const int AlbumResultLimit = 5;

    // The album search misses real albums the catalog has (ADR 0016), so the provider also asks for
    // songs and groups them by album; a wider limit here gives that pass a better chance of covering
    // the wanted album.
    private const int SongsForAlbumSearchLimit = 25;

    public Task<IReadOnlyList<ITunesItem>> SearchSongsAsync(string query, CancellationToken cancellationToken) =>
        SearchAsync(query, "song", TrackResultLimit, cancellationToken);

    public Task<IReadOnlyList<ITunesItem>> SearchAlbumsAsync(string query, CancellationToken cancellationToken) =>
        SearchAsync(query, "album", AlbumResultLimit, cancellationToken);

    public Task<IReadOnlyList<ITunesItem>> SearchSongsForAlbumsAsync(string query, CancellationToken cancellationToken) =>
        SearchAsync(query, "song", SongsForAlbumSearchLimit, cancellationToken);

    private async Task<IReadOnlyList<ITunesItem>> SearchAsync(string query, string entity, int limit, CancellationToken cancellationToken)
    {
        var uri = QueryHelpers.AddQueryString("search", new Dictionary<string, string?>
        {
            ["term"] = query,
            ["entity"] = entity,
            ["media"] = "music",
            ["limit"] = limit.ToString(),
            ["country"] = settings.Country,
        });

        var client = httpClientFactory.CreateClient(AppleMusicConfiguration.ApiHttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await StreamingSearchResponseReader.SendAsync(client, request, "Apple Music", cancellationToken);
        var page = await StreamingSearchResponseReader.ReadAsync(
            response, "Apple Music", logger, () => new ITunesSearchResponse(null, null), cancellationToken);
        return page.Results ?? [];
    }
}
