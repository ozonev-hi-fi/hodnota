using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Discogs;

public class DiscogsApiClientTests
{
    private static readonly Uri ApiBaseAddress = new("https://api.discogs.com/");

    // Regression test: DependencyInjection.AddCatalog calls this exact ParseAdd on the same
    // constant when configuring the named HttpClient — a value that fails to parse throws at
    // client-creation time (first search request), not at startup, so nothing else here catches it.
    [Fact]
    public void UserAgent_IsAValidHttpHeaderValue()
    {
        using var client = new HttpClient();

        var act = () => client.DefaultRequestHeaders.UserAgent.ParseAdd(DiscogsConfiguration.UserAgent);

        act.Should().NotThrow();
    }

    private static HttpResponseMessage SearchResponse() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new DiscogsSearchResponse([])),
    };

    private static (DiscogsApiClient Client, TestHttpMessageHandler Handler) NewClient()
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, DiscogsConfiguration.ApiHttpClientName, ApiBaseAddress);
        var client = new DiscogsApiClient(factory, NullLogger<DiscogsApiClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task SearchMastersAsync_SendsQueryMasterTypeAndPerPage()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(SearchResponse());

        await client.SearchMastersAsync("black album", CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.AbsolutePath.Should().EndWith("database/search");
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
        query["q"].ToString().Should().Be("black album");
        query["type"].ToString().Should().Be("master");
        query["per_page"].ToString().Should().Be("5");
    }

    [Fact]
    public async Task SearchReleasesAsync_SendsQueryReleaseTypeAndPerPage()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(SearchResponse());

        await client.SearchReleasesAsync("black album", CancellationToken.None);

        var query = QueryHelpers.ParseQuery(handler.Requests.Should().ContainSingle().Subject.RequestUri!.Query);
        query["q"].ToString().Should().Be("black album");
        query["type"].ToString().Should().Be("release");
        query["per_page"].ToString().Should().Be("25");
    }

    [Fact]
    public async Task SearchMastersAsync_QueryWithSpecialCharacters_IsUrlEncoded()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(SearchResponse());

        await client.SearchMastersAsync("metallica & friends?", CancellationToken.None);

        var query = QueryHelpers.ParseQuery(handler.Requests[0].RequestUri!.Query);
        query["q"].ToString().Should().Be("metallica & friends?");
    }

    [Fact]
    public async Task SearchMastersAsync_ErrorStatus_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var act = () => client.SearchMastersAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchMastersAsync_TooManyRequests_ThrowsStreamingProviderExceptionWithoutRetrying()
    {
        var (client, handler) = NewClient();
        var tooManyRequests = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooManyRequests.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
        handler.Enqueue(tooManyRequests);

        var act = () => client.SearchMastersAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchMastersAsync_MalformedJson_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json", System.Text.Encoding.UTF8, "application/json") });

        var act = () => client.SearchMastersAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchMastersAsync_SuccessfulEmptyResponse_ReturnsEmptyResult()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(SearchResponse());

        var result = await client.SearchMastersAsync("query", CancellationToken.None);

        result.Results.Should().BeEmpty();
    }
}
