using System.Net;
using System.Net.Http.Headers;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.Spotify;

public sealed class SpotifyApiClient(
    IHttpClientFactory httpClientFactory,
    SpotifyAccessTokenProvider tokenProvider,
    SpotifyCredentials credentials,
    ILogger<SpotifyApiClient> logger)
{
    private const int ResultLimitPerType = 5;

    public async Task<SpotifySearchResponse> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var response = await SendSearchRequestAsync(query, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            tokenProvider.Invalidate();
            response = await SendSearchRequestAsync(query, cancellationToken);
        }

        return await StreamingSearchResponseReader.ReadAsync(response, "Spotify", logger, () => new SpotifySearchResponse(null, null), cancellationToken);
    }

    private async Task<HttpResponseMessage> SendSearchRequestAsync(string query, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(SpotifyConfiguration.ApiHttpClientName);
        var token = await tokenProvider.GetTokenAsync(cancellationToken);

        var queryParams = new Dictionary<string, string?>
        {
            ["q"] = query,
            ["type"] = "track,album",
            ["limit"] = ResultLimitPerType.ToString(),
        };
        if (!string.IsNullOrEmpty(credentials.Market))
        {
            queryParams["market"] = credentials.Market;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString("search", queryParams));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            return await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new StreamingProviderException("Spotify search request failed.", ex);
        }
    }
}
