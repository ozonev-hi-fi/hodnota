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

    private static string?[] RequestedTypes(TestHttpMessageHandler handler) =>
        [.. handler.Requests.Select(request => QueryHelpers.ParseQuery(request.RequestUri!.Query)["type"].ToString())];

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
        handler.Enqueue(Ok(Master(10, "Metallica - Metallica")));
        handler.Enqueue(Ok(
            Release(20, "Metallica - Metallica", masterId: 10),
            Release(30, "Metallica - Live Rarity", masterId: 0),
            Release(40, "Metallica - Promo Rarity", masterId: null)));

        var results = await provider.SearchAsync("metallica", StreamingResultType.Release, CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("master:10", "release:30", "release:40");
        RequestedTypes(handler).Should().Equal("master", "release");
    }

    [Fact]
    public async Task SearchAsync_Release_CapsCombinedResultsAtFive()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok([.. Enumerable.Range(1, 4).Select(i => Master(i, $"Artist - Master {i}"))]));
        handler.Enqueue(Ok([.. Enumerable.Range(100, 5).Select(i => Release(i, $"Artist - Release {i}", masterId: 0))]));

        var results = await provider.SearchAsync("artist", StreamingResultType.Release, CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().Equal("master:1", "master:2", "master:3", "master:4", "release:100");
    }

    [Fact]
    public async Task SearchAsync_Release_FiveUsableMasters_DoesNotSendTheReleaseRequest()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok([.. Enumerable.Range(1, 5).Select(i => Master(i, $"Artist - Master {i}"))]));

        var results = await provider.SearchAsync("artist", StreamingResultType.Release, CancellationToken.None);

        results.Should().HaveCount(5);
        RequestedTypes(handler).Should().Equal("master");
    }

    [Fact]
    public async Task SearchAsync_Release_FiveMastersButOneUnusable_StillSendsTheReleaseRequest()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok([.. Enumerable.Range(1, 4).Select(i => Master(i, $"Artist - Master {i}")), Master(5, " ")]));
        handler.Enqueue(Ok(Release(100, "Artist - Release 100", masterId: 0)));

        var results = await provider.SearchAsync("artist", StreamingResultType.Release, CancellationToken.None);

        results.Select(r => r.Links.Single().ExternalId).Should().EndWith("release:100");
        RequestedTypes(handler).Should().Equal("master", "release");
    }

    [Fact]
    public async Task SearchAsync_Release_MasterRequestFails_ThrowsWithoutSendingTheReleaseRequest()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = () => provider.SearchAsync("query", StreamingResultType.Release, CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
        RequestedTypes(handler).Should().Equal("master");
    }

    [Fact]
    public async Task SearchAsync_Release_ReleaseRequestFails_ThrowsStreamingProviderException()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Master(10, "Metallica - Metallica")));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = () => provider.SearchAsync("query", StreamingResultType.Release, CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }
}
