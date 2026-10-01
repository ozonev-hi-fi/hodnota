using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Discogs;

public class DiscogsStreamingProviderLookupTests
{
    private static readonly Uri ApiBaseAddress = new("https://api.discogs.com/");

    private static (DiscogsStreamingProvider Provider, TestHttpMessageHandler Handler) NewProvider()
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, DiscogsConfiguration.ApiHttpClientName, ApiBaseAddress);
        return (new DiscogsStreamingProvider(new DiscogsApiClient(factory, NullLogger<DiscogsApiClient>.Instance)), handler);
    }

    private static HttpResponseMessage Ok(params DiscogsSearchResult[] results) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new DiscogsSearchResponse(results)) };

    private static DiscogsSearchResult Release(long id, long? masterId, params string[] barcodes) =>
        new(id, "release", "Radiohead - OK Computer", ["CD", "Album"], masterId, null, barcodes);

    private static StreamingLookupKey Key(params string[] codes) => new(StreamingResultType.Release, codes);

    [Fact]
    public async Task LookupAsync_SendsTheBarcodeAsAReleaseSearch()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Release(20, 10, "724385522925")));

        await provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        var query = QueryHelpers.ParseQuery(handler.Requests.Single().RequestUri!.Query);
        query["barcode"].ToString().Should().Be("724385522925");
        query["type"].ToString().Should().Be("release");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().EndWith("database/search");
    }

    [Fact]
    public async Task LookupAsync_ReleaseWithAMaster_LinksToTheMaster()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Release(20, masterId: 10, "724385522925")));

        var results = await provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Name.Should().Be("OK Computer");
        result.ArtistName.Should().Be("Radiohead");
        result.ReleaseType.Should().Be(ReleaseType.Album);
        result.Upc.Should().BeNull("Discogs barcodes are user-typed and never become a UPC");
        result.Links.Single().Should().Be(new ProviderLinkCandidate(PlatformCodes.Discogs, "master:10", new Uri("https://www.discogs.com/master/10")));
    }

    [Fact]
    public async Task LookupAsync_ReleaseWithoutAMaster_LinksToTheRelease()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Release(30, masterId: null, "724385522925")));

        var results = await provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        results.Single().Links.Single().Should().Be(new ProviderLinkCandidate(PlatformCodes.Discogs, "release:30", new Uri("https://www.discogs.com/release/30")));
    }

    [Fact]
    public async Task LookupAsync_PrefersAReleaseWithAMasterOverOneWithout()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Release(30, masterId: null, "724385522925"), Release(20, masterId: 10, "724385522925")));

        var results = await provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        results.First().Links.Single().ExternalId.Should().Be("master:10");
    }

    [Fact]
    public async Task LookupAsync_BarcodeTypedWithSpacesAndDashes_StillMatches()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Release(20, 10, "Matrix: A1", "7 24385-52292 5")));

        var results = await provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task LookupAsync_ResultWithAnotherBarcode_IsIgnored()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Release(20, 10, "5099902988085")));

        var results = await provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_NothingFound_AsksOnlyOnce()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok());

        var results = await provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        results.Should().BeEmpty();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task LookupAsync_ReleaseStoredWithTheOtherBarcodeForm_StillMatches()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(Release(20, 10, "0724385522925")));

        var results = await provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task LookupAsync_Track_ReturnsEmptyWithoutCallingDiscogs()
    {
        var (provider, handler) = NewProvider();

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["USRC17607839"]), CancellationToken.None);

        results.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_DiscogsFails_ThrowsStreamingProviderException()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var act = () => provider.LookupAsync(Key("724385522925", "0724385522925"), CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public void SupportsLookup_OnlyReleases()
    {
        var (provider, _) = NewProvider();

        provider.SupportsLookup(StreamingResultType.Release).Should().BeTrue();
        provider.SupportsLookup(StreamingResultType.Track).Should().BeFalse();
        provider.LinkPlatformCodes.Should().Equal(PlatformCodes.Discogs);
    }
}
