using System.Net;
using System.Text;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Providers.AppleMusic;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.AppleMusic;

// SearchAsync end to end through the real AppleMusicApiClient, with JSON trimmed from a live probe
// (2026-10-06, see ADR 0016) rather than built from the DTOs, so a field Apple stops sending shows
// up here instead of only in production.
public class AppleMusicStreamingProviderSearchTests
{
    private static readonly Uri ApiBaseAddress = new("https://itunes.apple.com/");

    // Trimmed from a real `search?term=metallica+enter+sandman&entity=song&country=US` response.
    private const string SongSearchJson = """
        {"resultCount":1,"results":[
          {"wrapperType":"track","kind":"song","trackId":1572051818,"collectionId":1572051816,
           "artistName":"Metallica","trackName":"Enter Sandman","collectionName":"Metallica (Deluxe Box Set)",
           "trackViewUrl":"https:\/\/music.apple.com\/us\/album\/enter-sandman\/1572051816?i=1572051818&uo=4",
           "artworkUrl100":"https:\/\/is1-ssl.mzstatic.com\/image\/thumb\/Music115\/v4\/x\/850007452056.png\/100x100bb.jpg",
           "isStreamable":true}
        ]}
        """;

    // Trimmed from a real `search?term=metallica+load&entity=album&country=US` response — does NOT
    // contain the real "Load (Remastered)" album (ADR 0016's finding).
    private const string AlbumSearchJson = """
        {"resultCount":1,"results":[
          {"wrapperType":"collection","collectionType":"Album","collectionId":1572046434,
           "artistName":"Metallica","collectionName":"Metallica (Remastered)",
           "collectionViewUrl":"https:\/\/music.apple.com\/us\/album\/metallica-remastered\/1572046434?uo=4",
           "artworkUrl100":"https:\/\/is1-ssl.mzstatic.com\/image\/thumb\/Music125\/v4\/y\/850007452025.png\/100x100bb.jpg"}
        ]}
        """;

    // Trimmed from a real `search?term=metallica+load&entity=song&country=US` response — the real
    // "Load (Remastered)" album surfaces here, via its tracks, even though the album search misses it.
    private const string SongsForAlbumSearchJson = """
        {"resultCount":1,"results":[
          {"wrapperType":"track","kind":"song","trackId":1806720500,"collectionId":1806720489,
           "artistName":"Metallica","trackName":"Until It Sleeps (Remastered)","collectionName":"Load (Remastered)",
           "collectionViewUrl":"https:\/\/music.apple.com\/us\/album\/load-remastered\/1806720489?i=1806720500&uo=4",
           "artworkUrl100":"https:\/\/is1-ssl.mzstatic.com\/image\/thumb\/Music221\/v4\/z\/810083963143.png\/100x100bb.jpg"}
        ]}
        """;

    private static AppleMusicStreamingProvider NewProvider(params string[] responses)
    {
        var handler = new TestHttpMessageHandler();
        foreach (var json in responses)
        {
            handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "text/javascript") });
        }

        var factory = TestHttpMessageHandler.CreateFactory(handler, AppleMusicConfiguration.ApiHttpClientName, ApiBaseAddress);
        var settings = new AppleMusicSettings("US");
        return new AppleMusicStreamingProvider(new AppleMusicApiClient(factory, settings, NullLogger<AppleMusicApiClient>.Instance), settings);
    }

    [Fact]
    public async Task SearchAsync_Track_MapsTheRealResponseShape()
    {
        var provider = NewProvider(SongSearchJson);

        var results = await provider.SearchAsync("metallica enter sandman", StreamingResultType.Track, CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Name.Should().Be("Enter Sandman");
        result.ArtistName.Should().Be("Metallica");
        result.Isrc.Should().BeNull();
        result.Links.Single().ExternalId.Should().Be("1572051818");
        result.ImageUrl!.ToString().Should().EndWith("600x600bb.jpg");
    }

    [Fact]
    public async Task SearchAsync_Release_MergesAlbumSearchWithAlbumsFoundViaSongs()
    {
        // Album search alone finds only "Metallica (Remastered)"; the real "Load (Remastered)"
        // surfaces only through the song search, which SearchAsync also makes for a Release query.
        var provider = NewProvider(AlbumSearchJson, SongsForAlbumSearchJson);

        var results = await provider.SearchAsync("metallica load", StreamingResultType.Release, CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Metallica (Remastered)");
        results[0].Upc.Should().BeNull();
        results[1].Name.Should().Be("Load (Remastered)");
        results[1].Links.Single().ExternalId.Should().Be("1806720489");
        // The song-derived URL's "?i=" track parameter must not leak into the album link.
        results[1].Links.Single().ExternalUrl.Query.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Release_AlbumSearchFillingTheCap_StillSurfacesTheSongDerivedMatch()
    {
        // Confirmed live (ADR 0016, 2026-10-06): a query like "metallica load" can get back 5 other
        // Metallica albums from entity=album alone (none of them "Load"), which used to fill the cap
        // before "Load (Remastered)" — found only via the song-grouped supplement — ever got a look.
        const string fullAlbumSearchJson = """
            {"resultCount":5,"results":[
              {"wrapperType":"collection","collectionType":"Album","collectionId":1,"artistName":"Metallica","collectionName":"Metallica (Remastered)"},
              {"wrapperType":"collection","collectionType":"Album","collectionId":2,"artistName":"Metallica","collectionName":"Ride the Lightning (Remastered)"},
              {"wrapperType":"collection","collectionType":"Album","collectionId":3,"artistName":"Metallica","collectionName":"Death Magnetic"},
              {"wrapperType":"collection","collectionType":"Album","collectionId":4,"artistName":"Metallica","collectionName":"72 Seasons"},
              {"wrapperType":"collection","collectionType":"Album","collectionId":5,"artistName":"Metallica","collectionName":"Metallica - Single"}
            ]}
            """;
        var provider = NewProvider(fullAlbumSearchJson, SongsForAlbumSearchJson);

        var results = await provider.SearchAsync("metallica load", StreamingResultType.Release, CancellationToken.None);

        results.Should().Contain(result => result.Name == "Load (Remastered)");
    }

    [Fact]
    public async Task SearchAsync_Release_DropsIrrelevantSongDerivedAlbums()
    {
        const string irrelevantSongJson = """
            {"resultCount":1,"results":[
              {"wrapperType":"track","kind":"song","trackId":1,"collectionId":2,
               "artistName":"Fronté","trackName":"Fronté News","collectionName":"Fronté News - Single",
               "collectionViewUrl":"https:\/\/music.apple.com\/us\/album\/fronte-news\/2?i=1"}
            ]}
            """;
        var provider = NewProvider("""{"resultCount":0,"results":[]}""", irrelevantSongJson);

        var results = await provider.SearchAsync("boards of canada music has the right to children", StreamingResultType.Release, CancellationToken.None);

        results.Should().BeEmpty();
    }
}
