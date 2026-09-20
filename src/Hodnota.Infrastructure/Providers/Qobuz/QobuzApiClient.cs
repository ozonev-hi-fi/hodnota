using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Hodnota.Infrastructure.Providers.Qobuz;

public sealed class QobuzApiClient(IHttpClientFactory httpClientFactory, ILogger<QobuzApiClient> logger)
{
    private const int ResultLimitPerType = 5;

    public async Task<QobuzSearchResponse> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(QobuzConfiguration.ApiHttpClientName);

        var queryParams = new Dictionary<string, string?>
        {
            ["query"] = query,
            ["limit"] = ResultLimitPerType.ToString(),
        };

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(QueryHelpers.AddQueryString("catalog/search", queryParams), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new StreamingProviderException("Qobuz search request failed.", ex);
        }

        return await StreamingSearchResponseReader.ReadAsync(response, "Qobuz", logger, () => new QobuzSearchResponse(null, null), cancellationToken);
    }
}
