using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Deezer;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Deezer;

public class DeezerStreamingProviderLookupTests
{
    private static readonly Uri ApiBaseAddress = new("https://api.deezer.com/");

    private static (DeezerStreamingProvider Provider, TestHttpMessageHandler Handler) NewProvider()
    {
        var handler = new TestHttpMessageHandler();
        var factory = TestHttpMessageHandler.CreateFactory(handler, DeezerConfiguration.ApiHttpClientName, ApiBaseAddress);
        return (new DeezerStreamingProvider(new DeezerApiClient(factory, NullLogger<DeezerApiClient>.Instance)), handler);
    }

    private static HttpResponseMessage Ok<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    [Fact]
    public async Task LookupAsync_Track_SendsTheIsrcAndMapsTheTrack()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(new DeezerTrack(1483825212, "Enter Sandman (Remastered 2021)", "QMKHM1900001", "https://www.deezer.com/track/1483825212", new DeezerArtist("Metallica"), null)));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["QMKHM1900001"]), CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Isrc.Should().Be("QMKHM1900001");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().EndWith("track/isrc:QMKHM1900001");
    }

    [Fact]
    public async Task LookupAsync_Track_ReturnedTrackWithAnotherIsrc_IsIgnored()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(new DeezerTrack(2, "Some Other Track", "GBAAA0000001", null, new DeezerArtist("Someone Else"), null)));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["QMKHM1900001"]), CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_Track_NotFound_ReturnsEmpty()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"error":{"type":"DataException","message":"no data","code":800}}""", System.Text.Encoding.UTF8, "application/json"),
        });

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["XX0000000000"]), CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_Release_TriesEachBarcodeFormInTurnAndStopsAtTheFirstMatch()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"error":{"type":"DataException","message":"no data","code":800}}""", System.Text.Encoding.UTF8, "application/json"),
        });
        handler.Enqueue(Ok(new DeezerAlbum(770314751, "Load (Remastered)", "602475158875", "https://www.deezer.com/album/770314751", "album", null, null, null, new DeezerArtist("Metallica"))));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Release, ["0602475158875", "602475158875", "00602475158875"]), CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Upc.Should().Be("602475158875");
        handler.Requests.Select(r => r.RequestUri!.AbsolutePath).Should().Equal(
            "/album/upc:0602475158875",
            "/album/upc:602475158875");
    }

    [Fact]
    public async Task LookupAsync_Release_ReturnedAlbumWithAnotherUpc_IsIgnored()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(Ok(new DeezerAlbum(1, "Unrelated", "111111111111", null, "album", null, null, null, null)));

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Release, ["602475158875"]), CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_Release_NoCodesFound_ReturnsEmpty()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"error":{"type":"DataException","message":"no data","code":800}}""", System.Text.Encoding.UTF8, "application/json"),
        });

        var results = await provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Release, ["000000000000"]), CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_DeezerFails_ThrowsStreamingProviderException()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"error":{"type":"Exception","message":"Quota limit exceeded","code":4}}""", System.Text.Encoding.UTF8, "application/json"),
        });

        var act = () => provider.LookupAsync(new StreamingLookupKey(StreamingResultType.Track, ["QMKHM1900001"]), CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }
}
