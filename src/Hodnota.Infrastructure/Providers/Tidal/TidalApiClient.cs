using System.Net;
using System.Net.Http.Headers;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers;

using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.Tidal;

public sealed class TidalApiClient(
    IHttpClientFactory httpClientFactory,
    TidalAccessTokenProvider tokenProvider,
    TidalCredentials credentials,
    ILogger<TidalApiClient> logger)
{
    // Artists and cover art come through include paths, so one request returns everything a result
    // needs. The commas must stay literal: an encoded comma makes Tidal read the list as one path.
    private const string TrackIncludes = "tracks,tracks.artists,tracks.albums.coverArt";
    private const string AlbumIncludes = "albums,albums.artists,albums.coverArt";

    public async Task<TidalSearchDocument> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        var response = await SendSearchRequestAsync(query, type, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            tokenProvider.Invalidate();
            response = await SendSearchRequestAsync(query, type, cancellationToken);
        }

        return await StreamingSearchResponseReader.ReadAsync(response, "Tidal", logger, () => new TidalSearchDocument(null, null), cancellationToken);
    }

    private async Task<HttpResponseMessage> SendSearchRequestAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(TidalConfiguration.ApiHttpClientName);
        var token = await tokenProvider.GetTokenAsync(cancellationToken);

        var uri = $"searchResults?filter%5Bquery%5D={Uri.EscapeDataString(query)}&include={(type == StreamingResultType.Track ? TrackIncludes : AlbumIncludes)}";
        if (!string.IsNullOrEmpty(credentials.CountryCode))
        {
            uri += $"&countryCode={Uri.EscapeDataString(credentials.CountryCode)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/vnd.api+json");

        return await StreamingSearchResponseReader.SendAsync(client, request, "Tidal", cancellationToken);
    }
}
