using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.Qobuz;

public sealed class QobuzApiClient(IHttpClientFactory httpClientFactory, ILogger<QobuzApiClient> logger)
{
    private const int ResultLimit = 5;

    public async Task<QobuzSearchResponse> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(QobuzConfiguration.ApiHttpClientName);

        var queryParams = new Dictionary<string, string?>
        {
            ["query"] = query,
            ["limit"] = ResultLimit.ToString(),
        };
        var endpoint = type == StreamingResultType.Track ? "track/search" : "album/search";

        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString(endpoint, queryParams));
        var response = await StreamingSearchResponseReader.SendAsync(client, request, "Qobuz", cancellationToken);

        return await StreamingSearchResponseReader.ReadAsync(response, "Qobuz", logger, () => new QobuzSearchResponse(null, null), cancellationToken);
    }
}
