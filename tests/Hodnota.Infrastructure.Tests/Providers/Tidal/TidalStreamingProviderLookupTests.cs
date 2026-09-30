using System.Net;
using System.Net.Http.Json;
using System.Text;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Tidal;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Tidal;

// A lookup answers with the tracks/albums themselves in `data`, unlike a search, whose `data` holds
// one searchResults item that points at them. The response shape follows Tidal's OpenAPI spec
// (tracks?filter[isrc]=, albums?filter[barcodeId]=); it has not been called live yet.
public class TidalStreamingProviderLookupTests
{
    private static readonly Uri ApiBaseAddress = new("https://openapi.tidal.com/v2/");
    private static readonly Uri AuthBaseAddress = new("https://auth.tidal.com/v1/");

    private static (TidalStreamingProvider Provider, TestHttpMessageHandler ApiHandler) NewProvider(params string[] apiResponses)
    {
        var apiHandler = new TestHttpMessageHandler();
        var authHandler = new TestHttpMessageHandler();
        authHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new TidalTokenResponse("token", 14400)) });
        foreach (var json in apiResponses)
        {
            apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/vnd.api+json") });
        }

        var factory = TestHttpMessageHandler.CreateFactory(
            (TidalConfiguration.ApiHttpClientName, new HttpClient(apiHandler, disposeHandler: false) { BaseAddress = ApiBaseAddress }),
            (TidalConfiguration.AuthHttpClientName, new HttpClient(authHandler, disposeHandler: false) { BaseAddress = AuthBaseAddress }));
        var credentials = new TidalCredentials("client-id", "client-secret", null);
        var tokenProvider = new TidalAccessTokenProvider(factory, credentials, TimeProvider.System);
        var apiClient = new TidalApiClient(factory, tokenProvider, credentials, NullLogger<TidalApiClient>.Instance);
        return (new TidalStreamingProvider(apiClient), apiHandler);
    }

    private static string TrackData(string id, string isrc) => """
        {"id":"@ID","type":"tracks","attributes":{"title":"Song @ID","version":null,"isrc":"@ISRC"},
         "relationships":{"artists":{"data":[{"id":"1","type":"artists"}]},"albums":{"data":[{"id":"9","type":"albums"}]}}}
        """.Replace("@ID", id).Replace("@ISRC", isrc);

    private static string AlbumData(string id, string barcode) => """
        {"id":"@ID","type":"albums","attributes":{"title":"Album @ID","barcodeId":"@BARCODE","albumType":"ALBUM"},
         "relationships":{"artists":{"data":[{"id":"1","type":"artists"}]}}}
        """.Replace("@ID", id).Replace("@BARCODE", barcode);

    private const string ArtistJson = """{"id":"1","type":"artists","attributes":{"name":"Metallica"}}""";

    private static string Document(string data, params string[] included) =>
        $$"""{"data":[{{data}}],"included":[{{string.Join(",", included)}}]}""";

    private static StreamingLookupKey TrackKey(params string[] codes) => new(StreamingResultType.Track, codes);

    private static StreamingLookupKey ReleaseKey(params string[] codes) => new(StreamingResultType.Release, codes);

    [Fact]
    public async Task LookupAsync_Track_SendsTheIsrcFilterAndMapsTheTrack()
    {
        var (provider, handler) = NewProvider(Document(TrackData("5", "USRC17607839"), ArtistJson));

        var results = await provider.LookupAsync(TrackKey("USRC17607839"), CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Name.Should().Be("Song 5");
        result.ArtistName.Should().Be("Metallica");
        result.Isrc.Should().Be("USRC17607839");
        result.Links.Single().ExternalId.Should().Be("5");
        var query = handler.Requests.Single().RequestUri!.Query;
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().EndWith("/tracks");
        query.Should().Contain("filter%5Bisrc%5D=USRC17607839");
        query.Should().Contain("include=artists,albums,albums.coverArt");
    }

    [Fact]
    public async Task LookupAsync_Track_IgnoresATrackWhoseIsrcDiffers()
    {
        var (provider, _) = NewProvider(Document($"{TrackData("5", "GBAAA0000001")},{TrackData("6", "usrc17607839")}", ArtistJson));

        var results = await provider.LookupAsync(TrackKey("USRC17607839"), CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("6");
    }

    [Fact]
    public async Task LookupAsync_Release_SendsTheBarcodeFilterAndMapsTheAlbum()
    {
        var (provider, handler) = NewProvider(Document(AlbumData("7", "602537817016"), ArtistJson));

        var results = await provider.LookupAsync(ReleaseKey("602537817016", "0602537817016"), CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Type.Should().Be(StreamingResultType.Release);
        result.Upc.Should().Be("602537817016");
        var request = handler.Requests.Single();
        request.RequestUri!.AbsolutePath.Should().EndWith("/albums");
        request.RequestUri.Query.Should().Contain("filter%5BbarcodeId%5D=602537817016");
        handler.Requests.Should().ContainSingle("the first form already matched");
    }

    [Fact]
    public async Task LookupAsync_Release_NothingForTheFirstFormTriesTheSecondForm()
    {
        var (provider, handler) = NewProvider(
            Document(AlbumData("8", "5099902988085")),
            Document(AlbumData("7", "0602537817016"), ArtistJson));

        var results = await provider.LookupAsync(ReleaseKey("602537817016", "0602537817016"), CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("7");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].RequestUri!.Query.Should().Contain("filter%5BbarcodeId%5D=0602537817016");
    }

    [Fact]
    public async Task LookupAsync_NoMatchForAnyForm_ReturnsEmpty()
    {
        var (provider, handler) = NewProvider("""{"data":[],"included":[]}""", "{}");

        var results = await provider.LookupAsync(ReleaseKey("602537817016", "0602537817016"), CancellationToken.None);

        results.Should().BeEmpty();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task LookupAsync_TidalFails_ThrowsStreamingProviderException()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var act = () => provider.LookupAsync(TrackKey("USRC17607839"), CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public void SupportsLookup_BothTypes()
    {
        var (provider, _) = NewProvider();

        provider.SupportsLookup(StreamingResultType.Track).Should().BeTrue();
        provider.SupportsLookup(StreamingResultType.Release).Should().BeTrue();
        provider.LinkPlatformCodes.Should().Equal(PlatformCodes.Tidal);
    }
}
