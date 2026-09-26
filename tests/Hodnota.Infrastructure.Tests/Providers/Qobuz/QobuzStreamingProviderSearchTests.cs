using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.Qobuz;
using Hodnota.Infrastructure.Tests.Providers;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hodnota.Infrastructure.Tests.Providers.Qobuz;

public class QobuzStreamingProviderSearchTests
{
    private static readonly Uri ApiBaseAddress = new("https://www.qobuz.com/api.json/0.2/");

    private static QobuzStreamingProvider NewProvider()
    {
        var handler = new TestHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new QobuzSearchResponse(
                new QobuzPagedResult<QobuzTrack>([new QobuzTrack(1, "Nothing Else Matters", null, new QobuzArtist("Metallica"), null)]),
                new QobuzPagedResult<QobuzAlbum>([new QobuzAlbum("album-1", "Metallica", null, new QobuzArtist("Metallica"), null, "album")]))),
        });
        var factory = TestHttpMessageHandler.CreateFactory(handler, QobuzConfiguration.ApiHttpClientName, ApiBaseAddress);
        return new QobuzStreamingProvider(new QobuzApiClient(factory, NullLogger<QobuzApiClient>.Instance));
    }

    [Fact]
    public async Task SearchAsync_Track_ReturnsOnlyTracks()
    {
        var results = await NewProvider().SearchAsync("metallica", StreamingResultType.Track, CancellationToken.None);

        results.Should().ContainSingle().Which.Type.Should().Be(StreamingResultType.Track);
    }

    [Fact]
    public async Task SearchAsync_Release_ReturnsOnlyAlbums()
    {
        var results = await NewProvider().SearchAsync("metallica", StreamingResultType.Release, CancellationToken.None);

        results.Should().ContainSingle().Which.Type.Should().Be(StreamingResultType.Release);
    }
}
