using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Spotify;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Spotify;

public class SpotifyApiClientTests
{
    private static readonly Uri ApiBaseAddress = new("https://api.spotify.com/v1/");
    private static readonly Uri AccountsBaseAddress = new("https://accounts.spotify.com/");

    private static HttpResponseMessage TokenResponse(string accessToken = "test-token") =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new SpotifyTokenResponse(accessToken, 3600)) };

    private static HttpResponseMessage SearchResponse() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new SpotifySearchResponse(
            new SpotifyPagedResult<SpotifyTrack>([]),
            new SpotifyPagedResult<SpotifyAlbum>([]))),
    };

    private static (SpotifyApiClient Client, TestHttpMessageHandler ApiHandler, TestHttpMessageHandler AccountsHandler) NewClient(string? market = null)
    {
        var apiHandler = new TestHttpMessageHandler();
        var accountsHandler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(
            (SpotifyConfiguration.ApiHttpClientName, new HttpClient(apiHandler, disposeHandler: false) { BaseAddress = ApiBaseAddress }),
            (SpotifyConfiguration.AccountsHttpClientName, new HttpClient(accountsHandler, disposeHandler: false) { BaseAddress = AccountsBaseAddress }));

        var credentials = new SpotifyCredentials("client-id", "client-secret", market);
        var tokenProvider = new SpotifyAccessTokenProvider(factory, credentials, TimeProvider.System);
        var client = new SpotifyApiClient(factory, tokenProvider, credentials, NullLogger<SpotifyApiClient>.Instance);
        return (client, apiHandler, accountsHandler);
    }

    [Fact]
    public async Task SearchAsync_SendsTrackAndAlbumTypesWithBearerToken()
    {
        var (client, apiHandler, accountsHandler) = NewClient();
        accountsHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("nothing else matters", CancellationToken.None);

        var request = apiHandler.Requests.Should().ContainSingle().Subject;
        var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
        query["q"].ToString().Should().Be("nothing else matters");
        query["type"].ToString().Should().Be("track,album");
        query["limit"].ToString().Should().Be("5");
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-token");
    }

    [Fact]
    public async Task SearchAsync_MarketConfigured_IncludesMarketParameter()
    {
        var (client, apiHandler, accountsHandler) = NewClient(market: "US");
        accountsHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("query", CancellationToken.None);

        var query = QueryHelpers.ParseQuery(apiHandler.Requests[0].RequestUri!.Query);
        query["market"].ToString().Should().Be("US");
    }

    [Fact]
    public async Task SearchAsync_MarketNotConfigured_OmitsMarketParameter()
    {
        var (client, apiHandler, accountsHandler) = NewClient();
        accountsHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("query", CancellationToken.None);

        var query = QueryHelpers.ParseQuery(apiHandler.Requests[0].RequestUri!.Query);
        query.Should().NotContainKey("market");
    }

    [Fact]
    public async Task SearchAsync_QueryWithSpecialCharacters_IsUrlEncoded()
    {
        var (client, apiHandler, accountsHandler) = NewClient();
        accountsHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("metallica & friends?", CancellationToken.None);

        var query = QueryHelpers.ParseQuery(apiHandler.Requests[0].RequestUri!.Query);
        query["q"].ToString().Should().Be("metallica & friends?");
    }

    [Fact]
    public async Task SearchAsync_Unauthorized_InvalidatesTokenAndRetriesOnce()
    {
        var (client, apiHandler, accountsHandler) = NewClient();
        accountsHandler.Enqueue(TokenResponse("token-1"));
        accountsHandler.Enqueue(TokenResponse("token-2"));
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("query", CancellationToken.None);

        apiHandler.Requests.Should().HaveCount(2);
        apiHandler.Requests[0].Headers.Authorization!.Parameter.Should().Be("token-1");
        apiHandler.Requests[1].Headers.Authorization!.Parameter.Should().Be("token-2");
    }

    [Fact]
    public async Task SearchAsync_UnauthorizedTwice_ThrowsStreamingProviderException()
    {
        var (client, apiHandler, accountsHandler) = NewClient();
        accountsHandler.Enqueue(TokenResponse("token-1"));
        accountsHandler.Enqueue(TokenResponse("token-2"));
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var act = () => client.SearchAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchAsync_TooManyRequests_ThrowsStreamingProviderExceptionWithoutRetrying()
    {
        var (client, apiHandler, accountsHandler) = NewClient();
        accountsHandler.Enqueue(TokenResponse());
        var tooManyRequests = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooManyRequests.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
        apiHandler.Enqueue(tooManyRequests);

        var act = () => client.SearchAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
        apiHandler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_MalformedJson_ThrowsStreamingProviderException()
    {
        var (client, apiHandler, accountsHandler) = NewClient();
        accountsHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json", System.Text.Encoding.UTF8, "application/json") });

        var act = () => client.SearchAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }
}
