using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Spotify;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Spotify;

public class SpotifyStreamingProviderLookupTests
{
    private static (SpotifyStreamingProvider Provider, TestHttpMessageHandler ApiHandler) NewProvider()
    {
        var apiHandler = new TestHttpMessageHandler();
        var accountsHandler = new TestHttpMessageHandler();
        accountsHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new SpotifyTokenResponse("token", 3600)) });
        var factory = TestHttpMessageHandler.CreateFactory(
            (SpotifyConfiguration.ApiHttpClientName, new HttpClient(apiHandler, disposeHandler: false) { BaseAddress = new Uri("https://api.spotify.com/v1/") }),
            (SpotifyConfiguration.AccountsHttpClientName, new HttpClient(accountsHandler, disposeHandler: false) { BaseAddress = new Uri("https://accounts.spotify.com/") }));
        var credentials = new SpotifyCredentials("client-id", "client-secret", null);
        var tokenProvider = new SpotifyAccessTokenProvider(factory, credentials, TimeProvider.System);
        var apiClient = new SpotifyApiClient(factory, tokenProvider, credentials, NullLogger<SpotifyApiClient>.Instance);
        return (new SpotifyStreamingProvider(apiClient), apiHandler);
    }

    private static HttpResponseMessage Ok(SpotifySearchResponse response) => new(HttpStatusCode.OK) { Content = JsonContent.Create(response) };

    private static SpotifyTrack Track(string id, string isrc) =>
        new(id, $"Song {id}", [new SpotifyArtist("Metallica")], null, null, new SpotifyExternalIds(isrc));

    private static SpotifyAlbum Album(string id) => new(id, $"Album {id}", "album", [new SpotifyArtist("Radiohead")], null, null);

    private static SpotifySearchResponse Tracks(params SpotifyTrack[] tracks) =>
        new(new SpotifyPagedResult<SpotifyTrack>(tracks), null);

    private static SpotifySearchResponse Albums(params SpotifyAlbum[] albums) =>
        new(null, new SpotifyPagedResult<SpotifyAlbum>(albums));

    [Fact]
    public async Task LookupAsync_Track_SearchesWithTheIsrcFilterAndKeepsOnlyTracksWithThatIsrc()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Tracks(Track("a", "GBAAA0000001"), Track("b", "USRC17607839"))));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["USRC17607839"]), CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("b");
        var query = QueryHelpers.ParseQuery(handler.Requests.Single().RequestUri!.Query);
        query["q"].ToString().Should().Be("isrc:USRC17607839");
        query["type"].ToString().Should().Be("track");
    }

    [Fact]
    public async Task LookupAsync_Release_SearchesWithTheUpcFilter()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Albums(Album("x"))));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Release, ["724385522925", "0724385522925"]), CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("x");
        var query = QueryHelpers.ParseQuery(handler.Requests.Single().RequestUri!.Query);
        query["q"].ToString().Should().Be("upc:724385522925");
        query["type"].ToString().Should().Be("album");
    }

    [Fact]
    public async Task LookupAsync_Release_NothingForTheFirstFormTriesTheSecondForm()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Albums()));
        handler.Enqueue(Ok(Albums(Album("x"))));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Release, ["724385522925", "0724385522925"]), CancellationToken.None);

        results.Should().ContainSingle();
        QueryHelpers.ParseQuery(handler.Requests[1].RequestUri!.Query)["q"].ToString().Should().Be("upc:0724385522925");
    }

    [Fact]
    public void SupportsLookup_BothTypes()
    {
        var (provider, _) = NewProvider();

        provider.SupportsLookup(StreamingResultType.Track).Should().BeTrue();
        provider.SupportsLookup(StreamingResultType.Release).Should().BeTrue();
        provider.LinkPlatformCodes.Should().Equal(PlatformCodes.Spotify);
    }
}
