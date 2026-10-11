using AwesomeAssertions;

using Google.Apis.YouTube.v3.Data;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Providers.YouTube;

using NSubstitute;

namespace Hodnota.Infrastructure.Tests.Providers.YouTube;

// SearchAsync is the search-page wiring (ADR 0018 Phase 6): every row now needs an intrinsic
// authenticity signal (YouTubeAuthenticity), replacing the old weak word-match filter.
public class YouTubeStreamingProviderSearchTests
{
    private static IYouTubeApiClient NewApiClient() => Substitute.For<IYouTubeApiClient>();

    private static SearchResult VideoResult(string videoId, string title, string channelTitle) => new()
    {
        Id = new ResourceId { VideoId = videoId },
        Snippet = new SearchResultSnippet { Title = title, ChannelTitle = channelTitle },
    };

    private static SearchResult PlaylistResult(string playlistId, string title, string channelTitle) => new()
    {
        Id = new ResourceId { PlaylistId = playlistId },
        Snippet = new SearchResultSnippet { Title = title, ChannelTitle = channelTitle },
    };

    [Fact]
    public async Task SearchAsync_Track_RejectsThePirateUploadAndKeepsTheTopicVideo()
    {
        var apiClient = NewApiClient();
        // The real 2026-10-01 search results for "nostromo ecce lex" (docs/youtube-music-search.md
        // section 1): a pirate full-album upload, an auto-generated "Mix", and the genuine Topic track.
        apiClient.SearchVideosAsync("nostromo ecce lex", null, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                VideoResult("pirate1", "Nostromo - Ecce Lex (Full Album)", "somuchdeath18"),
                VideoResult("mix1", "Mix - Nostromo", "YouTube Music"),
                VideoResult("topic1", "Ecce Lex", "Nostromo - Topic"),
            ]);
        var provider = new YouTubeStreamingProvider(apiClient, new YouTubeChannelResolver(apiClient));

        var results = await provider.SearchAsync("nostromo ecce lex", StreamingResultType.Track, CancellationToken.None);

        results.Should().ContainSingle().Which.Links.Should().Contain(l => l.ExternalId == "topic1");
    }

    [Fact]
    public async Task SearchAsync_Release_RejectsAUserPlaylistEvenWithAMatchingTitle()
    {
        var apiClient = NewApiClient();
        apiClient.SearchPlaylistsAsync("metalica load", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([PlaylistResult("PLE8F8DBAADF4DFD20", "Metallica · Load (Full Album)", "A Random User")]);
        var provider = new YouTubeStreamingProvider(apiClient, new YouTubeChannelResolver(apiClient));

        var results = await provider.SearchAsync("metalica load", StreamingResultType.Release, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Release_AcceptsAnOlakPlaylistOwnedByYouTube()
    {
        var apiClient = NewApiClient();
        apiClient.SearchPlaylistsAsync("metallica load", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([PlaylistResult("OLAK5uy_nf8K8yHuLThX0qvPGCIM2eBKndPale4vU", "Album - Load", "YouTube")]);
        var provider = new YouTubeStreamingProvider(apiClient, new YouTubeChannelResolver(apiClient));

        var results = await provider.SearchAsync("metallica load", StreamingResultType.Release, CancellationToken.None);

        results.Should().ContainSingle().Which.Links.Should().Contain(l => l.ExternalId == "OLAK5uy_nf8K8yHuLThX0qvPGCIM2eBKndPale4vU");
    }

    [Fact]
    public async Task FindByNameAsync_Track_GenuineTopicVideoMatchesTheArtistAndTitle()
    {
        var apiClient = NewApiClient();
        apiClient.SearchVideosAsync("Metallica Nothing Else Matters", null, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([VideoResult("v1", "Nothing Else Matters", "Metallica - Topic")]);
        var provider = new YouTubeStreamingProvider(apiClient, new YouTubeChannelResolver(apiClient));

        var results = await provider.FindByNameAsync("Metallica", "Nothing Else Matters", StreamingResultType.Track, [], CancellationToken.None);

        results.Should().ContainSingle().Which.Links.Should().Contain(l => l.ExternalId == "v1");
    }

    [Fact]
    public async Task FindByNameAsync_Release_NoChannelConfirmed_ReturnsNothing()
    {
        var apiClient = NewApiClient();
        apiClient.SearchPlaylistsAsync("Nostromo Ecce Lex", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        var provider = new YouTubeStreamingProvider(apiClient, new YouTubeChannelResolver(apiClient));

        // No known track titles at all: the channel resolver can't verify any candidate.
        var results = await provider.FindByNameAsync("Nostromo", "Ecce Lex", StreamingResultType.Release, [], CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindByNameAsync_Release_NoOlakFound_FallsBackToAVerifiedTopicTrack()
    {
        var apiClient = NewApiClient();
        apiClient.SearchPlaylistsAsync("Nostromo Ecce Lex", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        apiClient.SearchChannelsAsync("Nostromo - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new SearchResult { Id = new ResourceId { ChannelId = "UCjbNuWiJ2t_hO5Yu8Eihi0w" }, Snippet = new SearchResultSnippet { ChannelTitle = "Nostromo - Topic" } }]);
        apiClient.GetUploadsPlaylistIdAsync("UCjbNuWiJ2t_hO5Yu8Eihi0w", Arg.Any<CancellationToken>()).Returns("UUjbNuWiJ2t_hO5Yu8Eihi0w");
        apiClient.GetPlaylistItemsAsync("UUjbNuWiJ2t_hO5Yu8Eihi0w", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new PlaylistItem { Snippet = new PlaylistItemSnippet { Title = "Rude Awakening (Stripped)" } }]);
        apiClient.SearchVideosAsync("Ecce Lex", "UCjbNuWiJ2t_hO5Yu8Eihi0w", 10, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([VideoResult("v-ecce-lex", "Ecce Lex", "Nostromo - Topic")]);
        var provider = new YouTubeStreamingProvider(apiClient, new YouTubeChannelResolver(apiClient));

        var results = await provider.FindByNameAsync("Nostromo", "Ecce Lex", StreamingResultType.Release, ["Ecce Lex", "Rude Awakening"], CancellationToken.None);

        // The substitute is a Track result, labelled as a track (ADR 0018 D2), never the album.
        results.Should().ContainSingle().Which.Type.Should().Be(StreamingResultType.Track);
        results.Single().Links.Should().Contain(l => l.ExternalId == "v-ecce-lex" && l.PlatformCode == PlatformCodes.YouTubeMusic);
    }
}
