using System.Net;
using System.Net.Http.Json;
using System.Text;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Deezer;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Deezer;

public class DeezerApiClientTests
{
    private static readonly Uri ApiBaseAddress = new("https://api.deezer.com/");

    private static (DeezerApiClient Client, TestHttpMessageHandler Handler) NewClient()
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, DeezerConfiguration.ApiHttpClientName, ApiBaseAddress);
        var client = new DeezerApiClient(factory, NullLogger<DeezerApiClient>.Instance);
        return (client, handler);
    }

    private static HttpResponseMessage Ok<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    [Fact]
    public async Task SearchTracksAsync_SendsQueryAndLimit()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerPage<DeezerTrack>([], null)));

        await client.SearchTracksAsync("nothing else matters", CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.AbsolutePath.Should().EndWith("search/track");
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
        query["q"].ToString().Should().Be("nothing else matters");
        query["limit"].ToString().Should().Be("5");
    }

    [Fact]
    public async Task SearchAlbumsAsync_SendsQueryAndLimit()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerPage<DeezerAlbum>([], null)));

        await client.SearchAlbumsAsync("load", CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.AbsolutePath.Should().EndWith("search/album");
        QueryHelpers.ParseQuery(request.RequestUri.Query)["q"].ToString().Should().Be("load");
    }

    [Fact]
    public async Task GetTrackByIsrcAsync_SendsTheIsrcPath()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerTrack(1, "Enter Sandman", "QMKHM1900001", "https://www.deezer.com/track/1", null, null)));

        await client.GetTrackByIsrcAsync("QMKHM1900001", CancellationToken.None);

        handler.Requests.Should().ContainSingle().Subject.RequestUri!.AbsolutePath.Should().EndWith("track/isrc:QMKHM1900001");
    }

    [Fact]
    public async Task GetAlbumByUpcAsync_SendsTheUpcPath()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerAlbum(1, "Load", "602475158875", null, "album", null, null, null, null)));

        await client.GetAlbumByUpcAsync("602475158875", CancellationToken.None);

        handler.Requests.Should().ContainSingle().Subject.RequestUri!.AbsolutePath.Should().EndWith("album/upc:602475158875");
    }

    [Fact]
    public async Task GetTrackByIsrcAsync_NotFoundError_ReturnsNull()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerTrack(null, null, null, null, null, null, new DeezerError("DataException", "no data", 800))));

        var result = await client.GetTrackByIsrcAsync("XX0000000000", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAlbumByUpcAsync_NotFoundError_ReturnsNull()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerAlbum(null, null, null, null, null, null, null, null, null, new DeezerError("DataException", "no data", 800))));

        var result = await client.GetAlbumByUpcAsync("000000000000", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchTracksAsync_QuotaExceededError_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerPage<DeezerTrack>(null, new DeezerError("Exception", "Quota limit exceeded", 4))));

        var act = () => client.SearchTracksAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchTracksAsync_OtherErrorCode_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerPage<DeezerTrack>(null, new DeezerError("InvalidQueryException", "bad query", 600))));

        var act = () => client.SearchTracksAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchTracksAsync_HttpErrorStatus_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = () => client.SearchTracksAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchTracksAsync_MalformedJson_ThrowsStreamingProviderException()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json", Encoding.UTF8, "application/json") });

        var act = () => client.SearchTracksAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchTracksAsync_SuccessfulEmptyResponse_ReturnsEmptyResult()
    {
        var (client, handler) = NewClient();
        handler.Enqueue(Ok(new DeezerPage<DeezerTrack>([], null)));

        var result = await client.SearchTracksAsync("query", CancellationToken.None);

        result.Should().BeEmpty();
    }
}
