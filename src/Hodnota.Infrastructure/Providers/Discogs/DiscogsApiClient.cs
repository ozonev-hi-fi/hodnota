using Hodnota.Infrastructure.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.Discogs;

public sealed class DiscogsApiClient(IHttpClientFactory httpClientFactory, ILogger<DiscogsApiClient> logger)
{
    private const int MasterResultLimit = 5;
    private const int ReleaseResultLimit = 25;

    public Task<DiscogsSearchResponse> SearchMastersAsync(string query, CancellationToken cancellationToken) =>
        SearchAsync(query, "master", MasterResultLimit, cancellationToken);

    public Task<DiscogsSearchResponse> SearchReleasesAsync(string query, CancellationToken cancellationToken) =>
        SearchAsync(query, "release", ReleaseResultLimit, cancellationToken);

    private async Task<DiscogsSearchResponse> SearchAsync(string query, string type, int resultLimit, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(DiscogsConfiguration.ApiHttpClientName);

        var queryParams = new Dictionary<string, string?>
        {
            ["q"] = query,
            ["type"] = type,
            ["per_page"] = resultLimit.ToString(),
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString("database/search", queryParams));
        var response = await StreamingSearchResponseReader.SendAsync(client, request, "Discogs", cancellationToken);

        return await StreamingSearchResponseReader.ReadAsync(response, "Discogs", logger, () => new DiscogsSearchResponse(null), cancellationToken);
    }
}
