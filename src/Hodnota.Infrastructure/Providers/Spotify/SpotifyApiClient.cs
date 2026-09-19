using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hodnota.Application.Catalog;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.Spotify;

public sealed class SpotifyApiClient(
    IHttpClientFactory httpClientFactory,
    SpotifyAccessTokenProvider tokenProvider,
    SpotifyCredentials credentials,
    ILogger<SpotifyApiClient> logger)
{
    // One call covering both candidate types — one round trip, one rate-limit unit, for a track
    // list and an album list together. limit=5 is valid under both readings of Spotify's currently
    // self-contradictory docs (Default 5/Range 0-10 vs. the long-standing Default 20/Range 0-50),
    // and is the right size for a short candidate list anyway. See ADR 0011.
    private const int ResultLimitPerType = 5;

    public async Task<SpotifySearchResponse> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var response = await SendSearchRequestAsync(query, cancellationToken);
        try
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                tokenProvider.Invalidate();
                response = await SendSearchRequestAsync(query, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Do not sleep-and-retry here: blocking an interactive search is worse than a
                // shorter result list. CatalogSearchService's failure isolation drops Spotify for
                // this one query while other providers still answer. See ADR 0011.
                logger.LogWarning("Spotify search was rate-limited; Retry-After: {RetryAfter}.", response.Headers.RetryAfter?.Delta);
                throw new StreamingProviderException("Spotify search was rate-limited.", new HttpRequestException("429 Too Many Requests"));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new StreamingProviderException(
                    $"Spotify search request failed with status {(int)response.StatusCode}.",
                    new HttpRequestException(response.ReasonPhrase));
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<SpotifySearchResponse>(cancellationToken)
                    ?? new SpotifySearchResponse(null, null);
            }
            catch (JsonException ex)
            {
                throw new StreamingProviderException("Spotify search response could not be parsed.", ex);
            }
        }
        finally
        {
            response.Dispose();
        }
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
