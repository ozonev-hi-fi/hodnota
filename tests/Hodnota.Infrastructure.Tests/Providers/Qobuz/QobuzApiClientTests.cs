using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Qobuz;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Qobuz;

public class QobuzApiClientTests
{
    private static readonly Uri ApiBaseAddress = new("https://www.qobuz.com/api.json/0.2/");

    private static HttpResponseMessage SearchResponse() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new QobuzSearchResponse(
            new QobuzPagedResult<QobuzTrack>([]),
            new QobuzPagedResult<QobuzAlbum>([]))),
    };

    private static (QobuzApiClient Client, TestHttpMessageHandler Handler) NewClient()
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, QobuzConfiguration.ApiHttpClientName, ApiBaseAddress);
        var client = new QobuzApiClient(factory, NullLogger<QobuzApiClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task SearchAsync_SendsQueryAndLimit()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(SearchResponse());

        await client.SearchAsync("nothing else matters", CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.AbsolutePath.Should().EndWith("catalog/search");
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
        query["query"].ToString().Should().Be("nothing else matters");
        query["limit"].ToString().Should().Be("5");
        query.Should().NotContainKey("type");
    }

    [Fact]
    public async Task SearchAsync_QueryWithSpecialCharacters_IsUrlEncoded()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(SearchResponse());

        await client.SearchAsync("metallica & friends?", CancellationToken.None);

        var query = QueryHelpers.ParseQuery(handler.Requests[0].RequestUri!.Query);
        query["query"].ToString().Should().Be("metallica & friends?");
    }

    [Fact]
    public async Task SearchAsync_ErrorStatus_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var act = () => client.SearchAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchAsync_TooManyRequests_ThrowsStreamingProviderExceptionWithoutRetrying()
    {
        var (client, handler) = NewClient();
        var tooManyRequests = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooManyRequests.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
        handler.Enqueue(tooManyRequests);

        var act = () => client.SearchAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_MalformedJson_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json", System.Text.Encoding.UTF8, "application/json") });

        var act = () => client.SearchAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchAsync_SuccessfulEmptyResponse_ReturnsEmptyResult()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(SearchResponse());

        var result = await client.SearchAsync("query", CancellationToken.None);

        result.Tracks!.Items.Should().BeEmpty();
        result.Albums!.Items.Should().BeEmpty();
    }
}
