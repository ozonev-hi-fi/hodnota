using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hodnota.Application.Catalog;

using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers;

// Shared response handling for every raw-HttpClient provider (Spotify, Qobuz, and future ones):
// disposes the response, maps a 429 or any other non-success status and a JSON parse failure onto
// StreamingProviderException, and falls back to emptyResult() for a valid-but-empty response body.
internal static class StreamingSearchResponseReader
{
    public static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        string providerName,
        ILogger logger,
        Func<T> emptyResult,
        CancellationToken cancellationToken)
    {
        try
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("{Provider} search was rate-limited; Retry-After: {RetryAfter}.", providerName, response.Headers.RetryAfter?.Delta);
                throw new StreamingProviderException($"{providerName} search was rate-limited.", new HttpRequestException("429 Too Many Requests"));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new StreamingProviderException(
                    $"{providerName} search request failed with status {(int)response.StatusCode}.",
                    new HttpRequestException(response.ReasonPhrase));
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? emptyResult();
            }
            catch (JsonException ex)
            {
                throw new StreamingProviderException($"{providerName} search response could not be parsed.", ex);
            }
        }
        finally
        {
            response.Dispose();
        }
    }
}
