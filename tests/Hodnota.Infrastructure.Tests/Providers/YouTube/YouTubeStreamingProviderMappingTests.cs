using AwesomeAssertions;

using Google.Apis.YouTube.v3.Data;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.YouTube;

namespace Hodnota.Infrastructure.Tests.Providers.YouTube;

// YouTubeStreamingProvider.SearchAsync itself needs a real YouTubeService (no fake HTTP seam is
// worth building for one call), but Google's Data.SearchResult is a plain settable POCO, so the
// SearchResult -> StreamingSearchResult mapping (internal, see InternalsVisibleTo) is tested directly.
public class YouTubeStreamingProviderMappingTests
{
    private static SearchResult NewVideoResult(string videoId = "abc123") => new()
    {
        Id = new ResourceId { VideoId = videoId },
        Snippet = new SearchResultSnippet
        {
            Title = "Nothing Else Matters",
            ChannelTitle = "Metallica",
            Thumbnails = new ThumbnailDetails { Medium = new Thumbnail { Url = "https://example.com/medium.jpg" } },
        },
    };

    private static SearchResult NewPlaylistResult(string playlistId = "OLAK5uy_abc") => new()
    {
        Id = new ResourceId { PlaylistId = playlistId },
        Snippet = new SearchResultSnippet
        {
            Title = "Nothing",
            ChannelTitle = "N.E.R.D.",
            Thumbnails = new ThumbnailDetails(),
        },
    };

    [Fact]
    public void ToSearchResult_VideoResult_MapsToTrackWithBothDerivedLinks()
    {
        var result = YouTubeStreamingProvider.ToSearchResult(NewVideoResult("abc123"));

        result.Type.Should().Be(StreamingResultType.Track);
        result.Name.Should().Be("Nothing Else Matters");
        result.ArtistName.Should().Be("Metallica");
        result.ImageUrl.Should().Be(new Uri("https://example.com/medium.jpg"));
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.YouTube, "abc123", new Uri("https://www.youtube.com/watch?v=abc123")),
            new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, "abc123", new Uri("https://music.youtube.com/watch?v=abc123")),
        ]);
    }

    [Fact]
    public void ToSearchResult_PlaylistResult_MapsToReleaseWithBothDerivedLinks()
    {
        var result = YouTubeStreamingProvider.ToSearchResult(NewPlaylistResult("OLAK5uy_abc"));

        result.Type.Should().Be(StreamingResultType.Release);
        result.Name.Should().Be("Nothing");
        result.ArtistName.Should().Be("N.E.R.D.");
        result.ImageUrl.Should().BeNull();
        result.Links.Should().BeEquivalentTo(
        [
            new ProviderLinkCandidate(PlatformCodes.YouTube, "OLAK5uy_abc", new Uri("https://www.youtube.com/playlist?list=OLAK5uy_abc")),
            new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, "OLAK5uy_abc", new Uri("https://music.youtube.com/playlist?list=OLAK5uy_abc")),
        ]);
    }
}
