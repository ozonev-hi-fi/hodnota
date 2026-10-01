using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Qobuz;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Qobuz;

public class QobuzStreamingProviderLookupTests
{
    private static readonly Uri ApiBaseAddress = new("https://www.qobuz.com/api.json/0.2/");

    private static (QobuzStreamingProvider Provider, TestHttpMessageHandler Handler) NewProvider()
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, QobuzConfiguration.ApiHttpClientName, ApiBaseAddress);
        return (new QobuzStreamingProvider(new QobuzApiClient(factory, NullLogger<QobuzApiClient>.Instance)), handler);
    }

    private static HttpResponseMessage Ok(QobuzSearchResponse response) => new(HttpStatusCode.OK) { Content = JsonContent.Create(response) };

    private static QobuzTrack Track(int id, string isrc) => new(id, $"Song {id}", isrc, new QobuzArtist("Metallica"), null);

    private static QobuzAlbum Album(string id, string upc) => new(id, $"Album {id}", upc, new QobuzArtist("Radiohead"), null, "album");

    private static QobuzSearchResponse Tracks(params QobuzTrack[] tracks) => new(new QobuzPagedResult<QobuzTrack>(tracks), null);

    private static QobuzSearchResponse Albums(params QobuzAlbum[] albums) => new(null, new QobuzPagedResult<QobuzAlbum>(albums));

    private static string QueryOf(HttpRequestMessage request) => QueryHelpers.ParseQuery(request.RequestUri!.Query)["query"].ToString();

    [Fact]
    public async Task LookupAsync_Track_SearchesWithTheIsrcAndKeepsOnlyTracksWithThatIsrc()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Tracks(Track(1, "GBAAA0000001"), Track(2, "qmkhm1900008"), Track(3, "QMKHM1900008"))));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["QMKHM1900008"]), CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("2", "3");
        QueryOf(handler.Requests.Single()).Should().Be("QMKHM1900008");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().EndWith("track/search");
    }

    [Fact]
    public async Task LookupAsync_Release_AsksWithTheThirteenDigitFormFirst()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Albums(Album("0198009087938", "0198009087938"))));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Release, ["198009087938", "0198009087938"]), CancellationToken.None);

        results.Should().ContainSingle().Which.Upc.Should().Be("0198009087938");
        QueryOf(handler.Requests.Single()).Should().Be("0198009087938");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().EndWith("album/search");
    }

    [Fact]
    public async Task LookupAsync_Release_NothingForTheThirteenDigitFormTriesTheTwelveDigitForm()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Albums()));
        handler.Enqueue(Ok(Albums(Album("a1", "198009087938"))));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Release, ["198009087938", "0198009087938"]), CancellationToken.None);

        results.Should().ContainSingle();
        handler.Requests.Select(QueryOf).Should().Equal("0198009087938", "198009087938");
    }

    [Fact]
    public async Task LookupAsync_Release_AlbumsWithAnotherBarcodeAreIgnored()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Albums(Album("a1", "5099902988085"))));
        handler.Enqueue(Ok(Albums(Album("a2", "5099902988085"))));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Release, ["198009087938", "0198009087938"]), CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_QobuzFails_ThrowsStreamingProviderException()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var act = () => provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["QMKHM1900008"]), CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public void SupportsLookup_BothTypes()
    {
        var (provider, _) = NewProvider();

        provider.SupportsLookup(StreamingResultType.Track).Should().BeTrue();
        provider.SupportsLookup(StreamingResultType.Release).Should().BeTrue();
        provider.LinkPlatformCodes.Should().Equal(PlatformCodes.Qobuz);
    }
}
