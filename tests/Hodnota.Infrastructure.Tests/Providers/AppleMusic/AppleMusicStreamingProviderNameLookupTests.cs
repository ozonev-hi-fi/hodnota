using System.Net;
using System.Text;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Providers.AppleMusic;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.AppleMusic;

// There is no exact ISRC/UPC lookup (ADR 0016), so enrichment falls back to IStreamingNameLookup,
// the same path YouTube uses.
public class AppleMusicStreamingProviderNameLookupTests
{
    private static readonly Uri ApiBaseAddress = new("https://itunes.apple.com/");

    private static AppleMusicStreamingProvider NewProvider(string songSearchJson)
    {
        var handler = new TestHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(songSearchJson, Encoding.UTF8, "text/javascript") });
        var factory = TestHttpMessageHandler.CreateFactory(handler, AppleMusicConfiguration.ApiHttpClientName, ApiBaseAddress);
        var settings = new AppleMusicSettings("US");
        return new AppleMusicStreamingProvider(new AppleMusicApiClient(factory, settings, NullLogger<AppleMusicApiClient>.Instance), settings);
    }

    [Fact]
    public async Task FindByNameAsync_SameArtistAndTitle_ReturnsTheMatch()
    {
        const string json = """
            {"resultCount":1,"results":[
              {"wrapperType":"track","kind":"song","trackId":1,"collectionId":10,
               "artistName":"Metallica","trackName":"Enter Sandman",
               "trackViewUrl":"https:\/\/music.apple.com\/us\/album\/x\/10?i=1"}
            ]}
            """;
        var provider = NewProvider(json);

        var results = await provider.FindByNameAsync("Metallica", "Enter Sandman", StreamingResultType.Track, CancellationToken.None);

        results.Should().ContainSingle().Which.Links.Single().ExternalId.Should().Be("1");
    }

    [Fact]
    public async Task FindByNameAsync_DifferentArtist_ReturnsNoMatch()
    {
        const string json = """
            {"resultCount":1,"results":[
              {"wrapperType":"track","kind":"song","trackId":1,"collectionId":10,
               "artistName":"Metal Militia","trackName":"Enter Sandman",
               "trackViewUrl":"https:\/\/music.apple.com\/us\/album\/x\/10?i=1"}
            ]}
            """;
        var provider = NewProvider(json);

        var results = await provider.FindByNameAsync("Metallica", "Enter Sandman", StreamingResultType.Track, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindByNameAsync_DifferentTitle_ReturnsNoMatch()
    {
        const string json = """
            {"resultCount":1,"results":[
              {"wrapperType":"track","kind":"song","trackId":1,"collectionId":10,
               "artistName":"Metallica","trackName":"Master of Puppets",
               "trackViewUrl":"https:\/\/music.apple.com\/us\/album\/x\/10?i=1"}
            ]}
            """;
        var provider = NewProvider(json);

        var results = await provider.FindByNameAsync("Metallica", "Enter Sandman", StreamingResultType.Track, CancellationToken.None);

        results.Should().BeEmpty();
    }
}
