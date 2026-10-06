using System.Net;
using System.Text;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.AppleMusic;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.AppleMusic;

public class AppleMusicApiClientTests
{
    private static readonly Uri ApiBaseAddress = new("https://itunes.apple.com/");

    private static (AppleMusicApiClient Client, TestHttpMessageHandler Handler) NewClient(string country = "US")
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, AppleMusicConfiguration.ApiHttpClientName, ApiBaseAddress);
        var client = new AppleMusicApiClient(factory, new AppleMusicSettings(country), NullLogger<AppleMusicApiClient>.Instance);
        return (client, handler);
    }

    // The real response's Content-Type is "text/javascript", not "application/json" (ADR 0016); this
    // helper matches that instead of relying on JsonContent.Create's default header.
    private static HttpResponseMessage OkAsJavaScript<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "text/javascript"),
    };

    [Fact]
    public async Task SearchSongsAsync_SendsTermEntityMediaLimitAndCountry()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(OkAsJavaScript(new ITunesSearchResponse(0, [])));

        await client.SearchSongsAsync("metallica enter sandman", CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.AbsolutePath.Should().EndWith("search");
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
        query["term"].ToString().Should().Be("metallica enter sandman");
        query["entity"].ToString().Should().Be("song");
        query["media"].ToString().Should().Be("music");
        query["limit"].ToString().Should().Be("5");
        query["country"].ToString().Should().Be("US");
    }

    [Fact]
    public async Task SearchAlbumsAsync_SendsAlbumEntity()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(OkAsJavaScript(new ITunesSearchResponse(0, [])));

        await client.SearchAlbumsAsync("load", CancellationToken.None);

        var query = QueryHelpers.ParseQuery(handler.Requests.Single().RequestUri!.Query);
        query["entity"].ToString().Should().Be("album");
        query["limit"].ToString().Should().Be("5");
    }

    [Fact]
    public async Task SearchSongsForAlbumsAsync_SendsSongEntityWithAWiderLimit()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(OkAsJavaScript(new ITunesSearchResponse(0, [])));

        await client.SearchSongsForAlbumsAsync("load", CancellationToken.None);

        var query = QueryHelpers.ParseQuery(handler.Requests.Single().RequestUri!.Query);
        query["entity"].ToString().Should().Be("song");
        query["limit"].ToString().Should().Be("25");
    }

    [Fact]
    public async Task SearchSongsAsync_UsesTheConfiguredCountry()
    {
        var (client, handler) = NewClient(country: "GB");
        handler.Enqueue(OkAsJavaScript(new ITunesSearchResponse(0, [])));

        await client.SearchSongsAsync("query", CancellationToken.None);

        QueryHelpers.ParseQuery(handler.Requests.Single().RequestUri!.Query)["country"].ToString().Should().Be("GB");
    }

    [Fact]
    public async Task SearchSongsAsync_ParsesTheJavaScriptContentTypeBody()
    {
        var (client, handler) = NewClient();
        var item = new ITunesItem(null, "song", 1, null, "Metallica", "Enter Sandman", null, null, "https://music.apple.com/us/a/1?i=1", null, null, true);
        handler.Enqueue(OkAsJavaScript(new ITunesSearchResponse(1, [item])));

        var result = await client.SearchSongsAsync("query", CancellationToken.None);

        result.Should().ContainSingle().Which.TrackName.Should().Be("Enter Sandman");
    }

    [Fact]
    public async Task SearchSongsAsync_HttpErrorStatus_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var act = () => client.SearchSongsAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchSongsAsync_RateLimited_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var act = () => client.SearchSongsAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchSongsAsync_MalformedJson_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json", Encoding.UTF8, "text/javascript") });

        var act = () => client.SearchSongsAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchSongsAsync_SuccessfulEmptyResponse_ReturnsEmptyResult()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(OkAsJavaScript(new ITunesSearchResponse(0, [])));

        var result = await client.SearchSongsAsync("query", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchSongsAsync_NoResultsField_ReturnsEmptyResult()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(OkAsJavaScript(new ITunesSearchResponse(0, null)));

        var result = await client.SearchSongsAsync("query", CancellationToken.None);

        result.Should().BeEmpty();
    }
}
