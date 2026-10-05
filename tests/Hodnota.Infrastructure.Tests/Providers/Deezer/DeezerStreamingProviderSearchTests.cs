using System.Net;
using System.Text;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Providers.Deezer;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Deezer;

// SearchAsync end to end through the real DeezerApiClient, with JSON trimmed from a live probe
// (2026-10-02, see ADR 0015) rather than built from the DTOs, so a field Deezer stops sending shows
// up here instead of only in production.
public class DeezerStreamingProviderSearchTests
{
    private static readonly Uri ApiBaseAddress = new("https://api.deezer.com/");

    // Trimmed from a real `search/track?q=metallica+enter+sandman` response.
    private const string TrackSearchJson = """
        {"data":[
          {"id":1483825212,"title":"Enter Sandman (Remastered 2021)","isrc":"QMKHM1900001",
           "link":"https:\/\/www.deezer.com\/track\/1483825212",
           "artist":{"id":119,"name":"Metallica"},
           "album":{"id":256250622,"title":"Metallica (Remastered 2021)",
             "cover_medium":"https:\/\/cdn-images.dzcdn.net\/images\/cover\/4f2093c9d25852c8f1937ae5a47b99a6\/250x250-000000-80-0-0.jpg",
             "cover_big":"https:\/\/cdn-images.dzcdn.net\/images\/cover\/4f2093c9d25852c8f1937ae5a47b99a6\/500x500-000000-80-0-0.jpg",
             "cover_xl":"https:\/\/cdn-images.dzcdn.net\/images\/cover\/4f2093c9d25852c8f1937ae5a47b99a6\/1000x1000-000000-80-0-0.jpg"}}
        ],"total":1}
        """;

    // Trimmed from a real `search/album?q=metallica+load` response. No upc field — album search
    // never returns one (ADR 0015); only the exact UPC lookup does.
    private const string AlbumSearchJson = """
        {"data":[
          {"id":770314751,"title":"Load (Remastered)","link":"https:\/\/www.deezer.com\/album\/770314751",
           "record_type":"album",
           "cover_medium":"https:\/\/cdn-images.dzcdn.net\/images\/cover\/836a8c66bb7337ba74c71e9549bccf63\/250x250-000000-80-0-0.jpg",
           "cover_big":"https:\/\/cdn-images.dzcdn.net\/images\/cover\/836a8c66bb7337ba74c71e9549bccf63\/500x500-000000-80-0-0.jpg",
           "cover_xl":"https:\/\/cdn-images.dzcdn.net\/images\/cover\/836a8c66bb7337ba74c71e9549bccf63\/1000x1000-000000-80-0-0.jpg",
           "artist":{"id":119,"name":"Metallica"}},
          {"id":770317201,"title":"Load (Remastered Deluxe Box Set)","link":"https:\/\/www.deezer.com\/album\/770317201",
           "record_type":"album","artist":{"id":119,"name":"Metallica"}}
        ],"total":2}
        """;

    private static DeezerStreamingProvider NewProvider(string json)
    {
        var handler = new TestHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        var factory = TestHttpMessageHandler.CreateFactory(handler, DeezerConfiguration.ApiHttpClientName, ApiBaseAddress);
        return new DeezerStreamingProvider(new DeezerApiClient(factory, NullLogger<DeezerApiClient>.Instance));
    }

    [Fact]
    public async Task SearchAsync_Track_MapsTheRealResponseShape()
    {
        var provider = NewProvider(TrackSearchJson);

        var results = await provider.SearchAsync("metallica enter sandman", StreamingResultType.Track, CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Name.Should().Be("Enter Sandman (Remastered 2021)");
        result.ArtistName.Should().Be("Metallica");
        result.Isrc.Should().Be("QMKHM1900001");
        result.Links.Single().ExternalId.Should().Be("1483825212");
        result.ImageUrl!.ToString().Should().EndWith("500x500-000000-80-0-0.jpg");
    }

    [Fact]
    public async Task SearchAsync_Release_MapsTheRealResponseShapeWithNoUpc()
    {
        var provider = NewProvider(AlbumSearchJson);

        var results = await provider.SearchAsync("metallica load", StreamingResultType.Release, CancellationToken.None);

        results.Should().HaveCount(2);
        var first = results[0];
        first.Name.Should().Be("Load (Remastered)");
        first.Upc.Should().BeNull();
        first.ReleaseType.Should().Be(ReleaseType.Album);
    }
}
