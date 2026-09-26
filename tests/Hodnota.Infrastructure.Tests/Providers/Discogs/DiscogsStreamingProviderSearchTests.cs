using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Discogs;

public class DiscogsStreamingProviderSearchTests
{
    private static readonly Uri ApiBaseAddress = new("https://api.discogs.com/");

    private static (DiscogsStreamingProvider Provider, TestHttpMessageHandler Handler) NewProvider()
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, DiscogsConfiguration.ApiHttpClientName, ApiBaseAddress);
        return (new DiscogsStreamingProvider(new DiscogsApiClient(factory, NullLogger<DiscogsApiClient>.Instance)), handler);
    }

    // The master and release requests run in parallel, so each response is picked by the request's
    // own type parameter, not by queue order.
    private static void RespondByType(TestHttpMessageHandler handler, Func<bool, HttpResponseMessage> respond)
    {
        HttpResponseMessage Respond(HttpRequestMessage request) =>
            respond(QueryHelpers.ParseQuery(request.RequestUri!.Query)["type"] == "master");

        handler.Enqueue(Respond);
        handler.Enqueue(Respond);
    }

    private static HttpResponseMessage Ok(params DiscogsSearchResult[] results) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new DiscogsSearchResponse(results)) };

    private static DiscogsSearchResult Master(long id, string title) => new(id, "master", title, ["CD", "Album"], id, null);

    private static DiscogsSearchResult Release(long id, string title, long? masterId) => new(id, "release", title, ["CD", "Album"], masterId, null);

    [Fact]
    public async Task SearchAsync_Track_ReturnsEmptyWithoutCallingDiscogs()
    {
        var (provider, handler) = NewProvider();

        var results = await provider.SearchAsync("nothing else matters", StreamingResultType.Track, CancellationToken.None);

        results.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Release_ReturnsMastersFirstThenOnlyReleasesWithoutAMaster()
    {
        var (provider, handler) = NewProvider();
        RespondByType(handler, isMaster => isMaster
            ? Ok(Master(10, "Metallica - Metallica"))
            : Ok(
                Release(20, "Metallica - Metallica", masterId: 10),
                Release(30, "Metallica - Live Rarity", masterId: 0),
                Release(40, "Metallica - Promo Rarity", masterId: null)));

        var results = await provider.SearchAsync("metallica", StreamingResultType.Release, CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("master:10", "release:30", "release:40");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_Release_CapsCombinedResultsAtFive()
    {
        var (provider, handler) = NewProvider();
        RespondByType(handler, isMaster => isMaster
            ? Ok([.. Enumerable.Range(1, 4).Select(i => Master(i, $"Artist - Master {i}"))])
            : Ok([.. Enumerable.Range(100, 5).Select(i => Release(i, $"Artist - Release {i}", masterId: 0))]));

        var results = await provider.SearchAsync("artist", StreamingResultType.Release, CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("master:1", "master:2", "master:3", "master:4", "release:100");
    }

    [Fact]
    public async Task SearchAsync_Release_OneRequestFails_ThrowsStreamingProviderException()
    {
        var (provider, handler) = NewProvider();
        RespondByType(handler, isMaster => isMaster ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Ok());

        var act = () => provider.SearchAsync("query", StreamingResultType.Release, CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }
}
