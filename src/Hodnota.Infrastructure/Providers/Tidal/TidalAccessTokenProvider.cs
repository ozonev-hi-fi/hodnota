using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hodnota.Application.Catalog;

namespace Hodnota.Infrastructure.Providers.Tidal;

public sealed class TidalAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    TidalCredentials credentials,
    TimeProvider timeProvider) : IDisposable
{
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

    // Used only when the token response carries no usable expires_in, so a missing value does not
    // make every search request a new token.
    private const int DefaultLifetimeSeconds = 3600;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && timeProvider.GetUtcNow() < _expiresAtUtc)
            {
                return _accessToken;
            }

            var (token, expiresIn) = await RequestTokenAsync(cancellationToken);
            _accessToken = token;
            _expiresAtUtc = timeProvider.GetUtcNow() + TimeSpan.FromSeconds(expiresIn) - ExpiryMargin;
            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Called after a 401 from the API client: the cached token is treated as invalid so the next
    // GetTokenAsync call requests a fresh one instead of reusing the one that just failed.
    public void Invalidate() => _expiresAtUtc = DateTimeOffset.MinValue;

    public void Dispose() => _gate.Dispose();

    private async Task<(string Token, int ExpiresIn)> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(TidalConfiguration.AuthHttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth2/token")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.ClientId}:{credentials.ClientSecret}")));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new StreamingProviderException("Tidal token request failed.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new StreamingProviderException(
                    $"Tidal token request failed with status {(int)response.StatusCode}.",
                    new HttpRequestException(response.ReasonPhrase));
            }

            try
            {
                var body = await response.Content.ReadFromJsonAsync<TidalTokenResponse>(cancellationToken);
                return body is null || string.IsNullOrEmpty(body.AccessToken)
                    ? throw new StreamingProviderException("Tidal token response was empty.", new InvalidOperationException("Missing access_token."))
                    : (body.AccessToken, body.ExpiresIn > 0 ? body.ExpiresIn : DefaultLifetimeSeconds);
            }
            catch (JsonException ex)
            {
                throw new StreamingProviderException("Tidal token response could not be parsed.", ex);
            }
        }
    }
}
