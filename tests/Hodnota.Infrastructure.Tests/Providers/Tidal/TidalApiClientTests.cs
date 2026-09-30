using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Tidal;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Tidal;

public class TidalApiClientTests
{
    private static readonly Uri ApiBaseAddress = new("https://openapi.tidal.com/v2/");
    private static readonly Uri AuthBaseAddress = new("https://auth.tidal.com/v1/");

    private static HttpResponseMessage TokenResponse(string accessToken = "test-token") =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new TidalTokenResponse(accessToken, 14400)) };

    private static HttpResponseMessage SearchResponse() =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new TidalSearchDocument([], [])) };

    private static (TidalApiClient Client, TestHttpMessageHandler ApiHandler, TestHttpMessageHandler AuthHandler) NewClient(string? countryCode = null)
    {
        var apiHandler = new TestHttpMessageHandler();
        var authHandler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(
            (TidalConfiguration.ApiHttpClientName, new HttpClient(apiHandler, disposeHandler: false) { BaseAddress = ApiBaseAddress }),
            (TidalConfiguration.AuthHttpClientName, new HttpClient(authHandler, disposeHandler: false) { BaseAddress = AuthBaseAddress }));

        var credentials = new TidalCredentials("client-id", "client-secret", countryCode);
        var tokenProvider = new TidalAccessTokenProvider(factory, credentials, TimeProvider.System);
        var client = new TidalApiClient(factory, tokenProvider, credentials, NullLogger<TidalApiClient>.Instance);
        return (client, apiHandler, authHandler);
    }

    [Theory]
    [InlineData(StreamingResultType.Track, "tracks,tracks.artists,tracks.albums.coverArt")]
    [InlineData(StreamingResultType.Release, "albums,albums.artists,albums.coverArt")]
    public async Task SearchAsync_SendsOnlyTheRequestedTypeWithBearerTokenAndJsonApiAccept(StreamingResultType type, string expectedInclude)
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("nothing else matters", type, CancellationToken.None);

        var request = apiHandler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.AbsolutePath.Should().Be("/v2/searchResults");
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
        query["filter[query]"].ToString().Should().Be("nothing else matters");
        query["include"].ToString().Should().Be(expectedInclude);
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-token");
        request.Headers.Accept.Should().ContainSingle().Which.MediaType.Should().Be("application/vnd.api+json");
    }

    // An encoded comma makes Tidal read the whole include list as one path name and answer 400
    // (seen against the live API), so the commas must go out literally.
    [Fact]
    public async Task SearchAsync_SendsTheIncludeCommasUnencoded()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        var rawQuery = apiHandler.Requests[0].RequestUri!.Query;
        rawQuery.Should().Contain("include=tracks,tracks.artists,tracks.albums.coverArt");
        rawQuery.ToLowerInvariant().Should().NotContain("%2c");
    }

    [Fact]
    public async Task SearchAsync_CountryCodeConfigured_IncludesCountryCodeParameter()
    {
        var (client, apiHandler, authHandler) = NewClient(countryCode: "DE");
        authHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        QueryHelpers.ParseQuery(apiHandler.Requests[0].RequestUri!.Query)["countryCode"].ToString().Should().Be("DE");
    }

    [Fact]
    public async Task SearchAsync_CountryCodeNotConfigured_OmitsCountryCodeParameter()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        QueryHelpers.ParseQuery(apiHandler.Requests[0].RequestUri!.Query).Should().NotContainKey("countryCode");
    }

    [Fact]
    public async Task SearchAsync_QueryWithSpecialCharacters_IsUrlEncoded()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("metallica & friends?#", StreamingResultType.Track, CancellationToken.None);

        var query = QueryHelpers.ParseQuery(apiHandler.Requests[0].RequestUri!.Query);
        query["filter[query]"].ToString().Should().Be("metallica & friends?#");
        query["include"].ToString().Should().Be("tracks,tracks.artists,tracks.albums.coverArt");
    }

    [Fact]
    public async Task SearchAsync_Unauthorized_InvalidatesTokenAndRetriesOnce()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse("token-1"));
        authHandler.Enqueue(TokenResponse("token-2"));
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        apiHandler.Enqueue(SearchResponse());

        await client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        apiHandler.Requests.Should().HaveCount(2);
        apiHandler.Requests[0].Headers.Authorization!.Parameter.Should().Be("token-1");
        apiHandler.Requests[1].Headers.Authorization!.Parameter.Should().Be("token-2");
    }

    [Fact]
    public async Task SearchAsync_UnauthorizedTwice_ThrowsStreamingProviderException()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse("token-1"));
        authHandler.Enqueue(TokenResponse("token-2"));
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var act = () => client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchAsync_TooManyRequests_ThrowsStreamingProviderExceptionWithoutRetrying()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse());
        var tooManyRequests = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooManyRequests.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
        apiHandler.Enqueue(tooManyRequests);

        var act = () => client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
        apiHandler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_BadRequest_ThrowsStreamingProviderException()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var act = () => client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchAsync_MalformedJson_ThrowsStreamingProviderException()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json", System.Text.Encoding.UTF8, "application/vnd.api+json") });

        var act = () => client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchAsync_ReadsAResponseWithTheJsonApiContentType()
    {
        var (client, apiHandler, authHandler) = NewClient();
        authHandler.Enqueue(TokenResponse());
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":[{"id":"x","type":"searchResults"}],"included":[]}""", System.Text.Encoding.UTF8, "application/vnd.api+json"),
        });

        var document = await client.SearchAsync("query", StreamingResultType.Track, CancellationToken.None);

        document.Data.Should().ContainSingle().Which.Type.Should().Be("searchResults");
    }
}
