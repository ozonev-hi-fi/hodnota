using System.Net;
using System.Net.Http.Json;
using System.Text;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Tidal;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Tidal;

// End-to-end through TidalApiClient and the JSON:API document shape, with JSON trimmed from real
// responses of the live API (2026-09-30): `data` holds one searchResults item whose relationships
// list {id, type} references in relevance order, and `included` holds the full objects in a
// different order.
public class TidalStreamingProviderSearchTests
{
    private static readonly Uri ApiBaseAddress = new("https://openapi.tidal.com/v2/");
    private static readonly Uri AuthBaseAddress = new("https://auth.tidal.com/v1/");

    private static (TidalStreamingProvider Provider, TestHttpMessageHandler ApiHandler) NewProvider(string apiResponseJson)
    {
        var apiHandler = new TestHttpMessageHandler();
        var authHandler = new TestHttpMessageHandler();
        authHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new TidalTokenResponse("token", 14400)) });
        apiHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(apiResponseJson, Encoding.UTF8, "application/vnd.api+json"),
        });

        var factory = TestHttpMessageHandler.CreateFactory(
            (TidalConfiguration.ApiHttpClientName, new HttpClient(apiHandler, disposeHandler: false) { BaseAddress = ApiBaseAddress }),
            (TidalConfiguration.AuthHttpClientName, new HttpClient(authHandler, disposeHandler: false) { BaseAddress = AuthBaseAddress }));
        var credentials = new TidalCredentials("client-id", "client-secret", null);
        var tokenProvider = new TidalAccessTokenProvider(factory, credentials, TimeProvider.System);
        var apiClient = new TidalApiClient(factory, tokenProvider, credentials, NullLogger<TidalApiClient>.Instance);
        return (new TidalStreamingProvider(apiClient), apiHandler);
    }

    // Placeholders, not string interpolation: JSON closes with runs of braces that an interpolated
    // raw string literal cannot hold.
    private static string TrackJson(string id, string title, string artistId = "27420", string albumId = "109813970") => """
        {"id":"@ID","type":"tracks","attributes":{"title":"@TITLE","version":null,"isrc":"ISRC@ID","externalLinks":[{"href":"https://tidal.com/browse/track/@ID","meta":{"type":"TIDAL_SHARING"}}]},
         "relationships":{"artists":{"data":[{"id":"@ARTIST","type":"artists"}]},"albums":{"data":[{"id":"@ALBUM","type":"albums"}]}}}
        """
        .Replace("@ID", id)
        .Replace("@TITLE", title)
        .Replace("@ARTIST", artistId)
        .Replace("@ALBUM", albumId);

    private static string References(string type, params string[] ids) =>
        string.Join(",", ids.Select(id => """{"id":"@ID","type":"@TYPE"}""".Replace("@ID", id).Replace("@TYPE", type)));

    private static string Document(string relationshipName, string[] referencedIds, params string[] included) => """
        {"data":[{"id":"sr1","type":"searchResults","attributes":{"query":"q"},
          "relationships":{"@NAME":{"data":[@REFERENCES]}}}],
         "links":{"self":"/searchResults?filter%5Bquery%5D=q"},
         "included":[@INCLUDED]}
        """
        .Replace("@NAME", relationshipName)
        .Replace("@REFERENCES", References(relationshipName, referencedIds))
        .Replace("@INCLUDED", string.Join(",", included));

    private const string MetallicaJson = """{"id":"27420","type":"artists","attributes":{"name":"Metallica"}}""";

    private const string AlbumWithCoverJson = """
        {"id":"109813970","type":"albums","attributes":{"title":"Metallica","barcodeId":"00602577891953","albumType":"ALBUM"},
         "relationships":{"coverArt":{"data":[{"id":"art1","type":"artworks"}]}}}
        """;

    private const string ArtworkJson = """
        {"id":"art1","type":"artworks","attributes":{"mediaType":"IMAGE","files":[
          {"href":"https://resources.tidal.com/images/x/1280x1280.jpg","meta":{"width":1280,"height":1280}},
          {"href":"https://resources.tidal.com/images/x/320x320.jpg","meta":{"width":320,"height":320}},
          {"href":"https://resources.tidal.com/images/x/80x80.jpg","meta":{"width":80,"height":80}}]}}
        """;

    [Fact]
    public async Task SearchAsync_Track_ReturnsResultsInReferenceOrderNotIncludedOrder()
    {
        // Relevance order is 2, then 1 — the included list has them the other way round.
        var (provider, _) = NewProvider(Document(
            "tracks",
            ["2", "1"],
            TrackJson("1", "Second in relevance"),
            TrackJson("2", "First in relevance"),
            MetallicaJson,
            AlbumWithCoverJson,
            ArtworkJson));

        var results = await provider.SearchAsync("q", StreamingResultType.Track, CancellationToken.None);

        results.Select(r => r.Name).Should().Equal("First in relevance", "Second in relevance");
        results[0].ArtistName.Should().Be("Metallica");
        results[0].ImageUrl.Should().Be(new Uri("https://resources.tidal.com/images/x/320x320.jpg"));
        results[0].Isrc.Should().Be("ISRC2");
        results[0].Links.Single().Should().Be(new ProviderLinkCandidate(PlatformCodes.Tidal, "2", new Uri("https://tidal.com/browse/track/2")));
    }

    [Fact]
    public async Task SearchAsync_Track_KeepsOnlyTheFirstFiveResults()
    {
        var ids = Enumerable.Range(1, 7).Select(i => i.ToString()).ToArray();
        var (provider, _) = NewProvider(Document("tracks", ids, [.. ids.Select(id => TrackJson(id, $"Song {id}")), MetallicaJson, AlbumWithCoverJson, ArtworkJson]));

        var results = await provider.SearchAsync("q", StreamingResultType.Track, CancellationToken.None);

        results.Select(r => r.Name).Should().Equal("Song 1", "Song 2", "Song 3", "Song 4", "Song 5");
    }

    [Fact]
    public async Task SearchAsync_Track_SkipsAReferenceWithNoIncludedObject()
    {
        var (provider, _) = NewProvider(Document("tracks", ["1", "404", "2"], TrackJson("1", "One"), TrackJson("2", "Two"), MetallicaJson));

        var results = await provider.SearchAsync("q", StreamingResultType.Track, CancellationToken.None);

        results.Select(r => r.Name).Should().Equal("One", "Two");
    }

    [Fact]
    public async Task SearchAsync_Album_MapsAlbumsFromTheAlbumsRelationship()
    {
        var album = """
            {"id":"11764671","type":"albums","attributes":{"title":"Infections of a Different Kind (Step I)","barcodeId":"0044003199682","albumType":"ALBUM",
              "externalLinks":[{"href":"https://tidal.com/browse/album/11764671","meta":{"type":"TIDAL_SHARING"}}]},
             "relationships":{"artists":{"data":[{"id":"3454","type":"artists"}]},"coverArt":{"data":[{"id":"art1","type":"artworks"}]}}}
            """;
        var artist = """{"id":"3454","type":"artists","attributes":{"name":"AURORA"}}""";
        var (provider, handler) = NewProvider(Document("albums", ["11764671"], artist, album, ArtworkJson));

        var results = await provider.SearchAsync("aurora different kind", StreamingResultType.Release, CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Type.Should().Be(StreamingResultType.Release);
        result.Name.Should().Be("Infections of a Different Kind (Step I)");
        result.ArtistName.Should().Be("AURORA");
        result.Upc.Should().Be("0044003199682");
        result.ReleaseType.Should().Be(ReleaseType.Album);
        result.ImageUrl.Should().Be(new Uri("https://resources.tidal.com/images/x/320x320.jpg"));
        handler.Requests.Single().RequestUri!.Query.Should().Contain("include=albums,albums.artists,albums.coverArt");
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmpty()
    {
        var (provider, _) = NewProvider("""{"data":[{"id":"sr1","type":"searchResults","relationships":{"tracks":{"data":[]}}}],"included":[]}""");

        var results = await provider.SearchAsync("q", StreamingResultType.Track, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_EmptyDocument_ReturnsEmpty()
    {
        var (provider, _) = NewProvider("{}");

        var results = await provider.SearchAsync("q", StreamingResultType.Release, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Supports_BothTypes()
    {
        var (provider, _) = NewProvider("{}");

        provider.Supports(StreamingResultType.Track).Should().BeTrue();
        provider.Supports(StreamingResultType.Release).Should().BeTrue();
        provider.ProviderCode.Should().Be(ProviderCodes.Tidal);
    }
}
