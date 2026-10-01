using Hodnota.Infrastructure.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.Discogs;

public sealed class DiscogsApiClient(IHttpClientFactory httpClientFactory, ILogger<DiscogsApiClient> logger)
{
    private const int MasterResultLimit = 5;
    private const int ReleaseResultLimit = 25;

    public Task<DiscogsSearchResponse> SearchMastersAsync(string query, CancellationToken cancellationToken) =>
        GetAsync(new Dictionary<string, string?> { ["q"] = query, ["type"] = "master", ["per_page"] = MasterResultLimit.ToString() }, cancellationToken);

    public Task<DiscogsSearchResponse> SearchReleasesAsync(string query, CancellationToken cancellationToken) =>
        GetAsync(new Dictionary<string, string?> { ["q"] = query, ["type"] = "release", ["per_page"] = ReleaseResultLimit.ToString() }, cancellationToken);

    public Task<DiscogsSearchResponse> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken) =>
        GetAsync(new Dictionary<string, string?> { ["barcode"] = barcode, ["type"] = "release", ["per_page"] = ReleaseResultLimit.ToString() }, cancellationToken);

    private async Task<DiscogsSearchResponse> GetAsync(Dictionary<string, string?> queryParams, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(DiscogsConfiguration.ApiHttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString("database/search", queryParams));
        var response = await StreamingSearchResponseReader.SendAsync(client, request, "Discogs", cancellationToken);

        return await StreamingSearchResponseReader.ReadAsync(response, "Discogs", logger, () => new DiscogsSearchResponse(null), cancellationToken);
    }
}
