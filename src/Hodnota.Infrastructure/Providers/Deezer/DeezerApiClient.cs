using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.Deezer;

public sealed class DeezerApiClient(IHttpClientFactory httpClientFactory, ILogger<DeezerApiClient> logger)
{
    private const int ResultLimit = 5;

    // Deezer's quota-exceeded error code (ADR 0015); handled like every other provider's 429.
    private const int QuotaExceededCode = 4;

    // Deezer's "no data" error code (ADR 0015); not a failure, just an empty/missing result.
    private const int NotFoundCode = 800;

    public async Task<IReadOnlyList<DeezerTrack>> SearchTracksAsync(string query, CancellationToken cancellationToken)
    {
        var page = await GetAsync("search/track", query, () => new DeezerPage<DeezerTrack>(null, null), cancellationToken);
        ThrowIfError(page.Error);
        return page.Data ?? [];
    }

    public async Task<IReadOnlyList<DeezerAlbum>> SearchAlbumsAsync(string query, CancellationToken cancellationToken)
    {
        var page = await GetAsync("search/album", query, () => new DeezerPage<DeezerAlbum>(null, null), cancellationToken);
        ThrowIfError(page.Error);
        return page.Data ?? [];
    }

    public async Task<DeezerTrack?> GetTrackByIsrcAsync(string isrc, CancellationToken cancellationToken)
    {
        var track = await GetAsync($"track/isrc:{Uri.EscapeDataString(isrc)}", () => new DeezerTrack(null, null, null, null, null, null), cancellationToken);
        ThrowIfError(track.Error);
        return track.Error is null ? track : null;
    }

    public async Task<DeezerAlbum?> GetAlbumByUpcAsync(string code, CancellationToken cancellationToken)
    {
        var album = await GetAsync($"album/upc:{Uri.EscapeDataString(code)}", () => new DeezerAlbum(null, null, null, null, null, null, null, null, null), cancellationToken);
        ThrowIfError(album.Error);
        return album.Error is null ? album : null;
    }

    // Translates Deezer's in-body error (ADR 0015) the same way StreamingSearchResponseReader
    // translates a transport-level 429/non-success status: log and throw, no retry. A 800 ("no
    // data") is not an error here — it means an empty or missing result, handled by the caller.
    private void ThrowIfError(DeezerError? error)
    {
        if (error is null || error.Code == NotFoundCode)
        {
            return;
        }

        if (error.Code == QuotaExceededCode)
        {
            logger.LogWarning("Deezer was rate-limited: {Message}.", error.Message);
            throw new StreamingProviderException("Deezer was rate-limited.", new HttpRequestException($"Deezer quota exceeded: {error.Message}"));
        }

        throw new StreamingProviderException($"Deezer request failed with code {error.Code}: {error.Message}.", new HttpRequestException(error.Message));
    }

    private Task<DeezerPage<T>> GetAsync<T>(string endpoint, string query, Func<DeezerPage<T>> emptyResult, CancellationToken cancellationToken) =>
        GetAsync(QueryHelpers.AddQueryString(endpoint, new Dictionary<string, string?> { ["q"] = query, ["limit"] = ResultLimit.ToString() }), emptyResult, cancellationToken);

    private async Task<T> GetAsync<T>(string uri, Func<T> emptyResult, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(DeezerConfiguration.ApiHttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await StreamingSearchResponseReader.SendAsync(client, request, "Deezer", cancellationToken);
        return await StreamingSearchResponseReader.ReadAsync(response, "Deezer", logger, emptyResult, cancellationToken);
    }
}
