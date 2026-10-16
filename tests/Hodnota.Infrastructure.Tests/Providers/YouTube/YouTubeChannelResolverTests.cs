using AwesomeAssertions;

using Google;
using Google.Apis.Requests;
using Google.Apis.YouTube.v3.Data;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Providers.YouTube;

using NSubstitute;

namespace Hodnota.Infrastructure.Tests.Providers.YouTube;

// Golden cases from docs/youtube-music-search.md E10: several "Artist - Topic" channels can share a
// name, and the validated disambiguator is overlap between a candidate's own sampled uploads and the
// catalog's already-known track names — never the distributor/label alone (the Kirk Hammett/Blackened
// Recordings counterexample).
public class YouTubeChannelResolverTests
{
    private static SearchResult Channel(string channelId, string title) => new()
    {
        Id = new ResourceId { ChannelId = channelId },
        Snippet = new SearchResultSnippet { ChannelTitle = title },
    };

    private static PlaylistItem Item(string title) => new() { Snippet = new PlaylistItemSnippet { Title = title } };

    private static IYouTubeApiClient NewApiClient() => Substitute.For<IYouTubeApiClient>();

    private static void SetUpChannel(IYouTubeApiClient apiClient, string channelId, string uploadsPlaylistId, params string[] uploadTitles)
    {
        apiClient.GetUploadsPlaylistIdAsync(channelId, Arg.Any<CancellationToken>()).Returns(uploadsPlaylistId);
        apiClient.GetPlaylistItemsAsync(uploadsPlaylistId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([.. uploadTitles.Select(Item)]);
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_NoKnownTrackTitles_ReturnsNullWithoutCallingTheApi()
    {
        var apiClient = NewApiClient();
        var resolver = new YouTubeChannelResolver(apiClient);

        var result = await resolver.ResolveTopicChannelAsync("Nostromo", [], CancellationToken.None);

        result.Should().BeNull();
        await apiClient.DidNotReceiveWithAnyArgs().SearchChannelsAsync(default!, default, default);
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_OneCandidateOverlapsKnownTracks_ReturnsThatChannel()
    {
        var apiClient = NewApiClient();
        apiClient.SearchChannelsAsync("Nostromo - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Channel("UCjbNuWiJ2t_hO5Yu8Eihi0w", "Nostromo - Topic")]);
        SetUpChannel(apiClient, "UCjbNuWiJ2t_hO5Yu8Eihi0w", "UUjbNuWiJ2t_hO5Yu8Eihi0w",
            "Selfish Blues (Stripped)", "End's Eve (Stripped)", "Rude Awakening (Stripped)");
        var resolver = new YouTubeChannelResolver(apiClient);

        var result = await resolver.ResolveTopicChannelAsync("Nostromo", ["Rude Awakening", "Turned Black"], CancellationToken.None);

        result.Should().Be("UCjbNuWiJ2t_hO5Yu8Eihi0w");
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_SameNamedCollidingChannelFirst_SkipsItAndFindsTheRealOne()
    {
        // RC8/E10: searching "Nostromo - Topic" also returns unrelated same-named acts.
        var apiClient = NewApiClient();
        apiClient.SearchChannelsAsync("Nostromo - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Channel("UC-synthpop-nostromo", "Nostromo - Topic"), Channel("UCjbNuWiJ2t_hO5Yu8Eihi0w", "Nostromo - Topic")]);
        SetUpChannel(apiClient, "UC-synthpop-nostromo", "UU-synthpop-nostromo", "Unrelated Synth Track", "Another Unrelated Track");
        SetUpChannel(apiClient, "UCjbNuWiJ2t_hO5Yu8Eihi0w", "UUjbNuWiJ2t_hO5Yu8Eihi0w", "Rude Awakening (Stripped)");
        var resolver = new YouTubeChannelResolver(apiClient);

        var result = await resolver.ResolveTopicChannelAsync("Nostromo", ["Rude Awakening"], CancellationToken.None);

        result.Should().Be("UCjbNuWiJ2t_hO5Yu8Eihi0w");
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_LabelAloneDoesNotPickAnUnrelatedArtistsChannel()
    {
        // E10: Kirk Hammett's own solo-album Topic channel shares Metallica's label (Blackened
        // Recordings) but has none of Metallica's own track titles — label/distributor is explicitly
        // not a sufficient signal (ADR 0018), so this must not be picked.
        var apiClient = NewApiClient();
        apiClient.SearchChannelsAsync("Metallica - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Channel("UC-kirk-hammett", "Kirk Hammett - Topic"), Channel("UCGexNm_Kw4rdQjLxmpb2EKw", "Metallica - Topic")]);
        SetUpChannel(apiClient, "UC-kirk-hammett", "UU-kirk-hammett", "Portals (Kirk Hammett solo track)");
        SetUpChannel(apiClient, "UCGexNm_Kw4rdQjLxmpb2EKw", "UUGexNm_Kw4rdQjLxmpb2EKw", "King Nothing", "Until It Sleeps");
        var resolver = new YouTubeChannelResolver(apiClient);

        var result = await resolver.ResolveTopicChannelAsync("Metallica", ["King Nothing", "Until It Sleeps"], CancellationToken.None);

        result.Should().Be("UCGexNm_Kw4rdQjLxmpb2EKw");
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_NonTopicCandidate_IsSkippedWithoutSampling()
    {
        var apiClient = NewApiClient();
        apiClient.SearchChannelsAsync("Metallica - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Channel("UC-plain-metallica", "Metallica")]);
        var resolver = new YouTubeChannelResolver(apiClient);

        var result = await resolver.ResolveTopicChannelAsync("Metallica", ["King Nothing"], CancellationToken.None);

        result.Should().BeNull();
        await apiClient.DidNotReceiveWithAnyArgs().GetUploadsPlaylistIdAsync(default!, default);
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_CandidateUploadsPlaylist404s_SkipsItAndContinues()
    {
        var apiClient = NewApiClient();
        apiClient.SearchChannelsAsync("Nostromo - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Channel("UC-broken", "Nostromo - Topic"), Channel("UCjbNuWiJ2t_hO5Yu8Eihi0w", "Nostromo - Topic")]);
        apiClient.GetUploadsPlaylistIdAsync("UC-broken", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string?>(new StreamingProviderException("YouTube channels request failed.", new InvalidOperationException())));
        SetUpChannel(apiClient, "UCjbNuWiJ2t_hO5Yu8Eihi0w", "UUjbNuWiJ2t_hO5Yu8Eihi0w", "Rude Awakening (Stripped)");
        var resolver = new YouTubeChannelResolver(apiClient);

        var result = await resolver.ResolveTopicChannelAsync("Nostromo", ["Rude Awakening"], CancellationToken.None);

        result.Should().Be("UCjbNuWiJ2t_hO5Yu8Eihi0w");
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_NoCandidateOverlaps_ReturnsNull()
    {
        var apiClient = NewApiClient();
        apiClient.SearchChannelsAsync("Nostromo - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Channel("UC-unrelated", "Nostromo - Topic")]);
        SetUpChannel(apiClient, "UC-unrelated", "UU-unrelated", "Completely Unrelated Song");
        var resolver = new YouTubeChannelResolver(apiClient);

        var result = await resolver.ResolveTopicChannelAsync("Nostromo", ["Rude Awakening"], CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_ImpostorUploadOnlyContainsAKnownShortTitle_IsNotPicked()
    {
        var apiClient = NewApiClient();
        apiClient.SearchChannelsAsync("Metallica - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Channel("UC-impostor", "Metallica - Topic"), Channel("UCGexNm_Kw4rdQjLxmpb2EKw", "Metallica - Topic")]);
        SetUpChannel(apiClient, "UC-impostor", "UU-impostor", "Alone", "Someone Else");
        SetUpChannel(apiClient, "UCGexNm_Kw4rdQjLxmpb2EKw", "UUGexNm_Kw4rdQjLxmpb2EKw", "One (Remastered)");
        var resolver = new YouTubeChannelResolver(apiClient);

        var result = await resolver.ResolveTopicChannelAsync("Metallica", ["One"], CancellationToken.None);

        result.Should().Be("UCGexNm_Kw4rdQjLxmpb2EKw");
    }

    [Fact]
    public async Task ResolveTopicChannelAsync_QuotaExceeded_ThrowsWithoutTryingTheNextCandidate()
    {
        var apiClient = NewApiClient();
        apiClient.SearchChannelsAsync("Nostromo - Topic", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Channel("UC-first", "Nostromo - Topic"), Channel("UCjbNuWiJ2t_hO5Yu8Eihi0w", "Nostromo - Topic")]);
        var quotaError = new GoogleApiException("youtube", "Quota exceeded.")
        {
            Error = new RequestError { Errors = [new SingleError { Reason = "quotaExceeded" }] },
        };
        apiClient.GetUploadsPlaylistIdAsync("UC-first", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string?>(new StreamingProviderException("YouTube channels request failed.", quotaError)));
        var resolver = new YouTubeChannelResolver(apiClient);

        var act = () => resolver.ResolveTopicChannelAsync("Nostromo", ["Rude Awakening"], CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
        await apiClient.DidNotReceive().GetUploadsPlaylistIdAsync("UCjbNuWiJ2t_hO5Yu8Eihi0w", Arg.Any<CancellationToken>());
    }
}
