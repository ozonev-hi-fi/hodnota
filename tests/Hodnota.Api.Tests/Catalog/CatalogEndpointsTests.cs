using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;
using Hodnota.Infrastructure.Catalog;

using RowState = Hodnota.Contracts.Catalog.PlatformRowState;

namespace Hodnota.Api.Tests.Catalog;

public class CatalogEndpointsTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>, IAsyncLifetime
{
    private const string Password = "P@ssw0rd!123";

    private readonly HttpClient _anonymousClient = factory.CreateClient();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = await CreateAuthenticatedClientAsync(factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Search_ReturnsCandidatesFromRegisteredProvider()
    {
        factory.SpotifyProvider.Results = [];
        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                new Uri("https://example.com/image.jpg"),
                [new ProviderLinkCandidate(PlatformCodes.YouTube, "search-1", new Uri("https://www.youtube.com/watch?v=search-1"))]),
        ];

        var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = await response.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
        candidates.Should().ContainSingle();
        candidates![0].Name.Should().Be("Nothing Else Matters");
        candidates[0].Artist.Should().Be("Metallica");
        candidates[0].Type.Should().Be(CandidateType.Song);
    }

    [Fact]
    public async Task Search_SameResultFromBothProviders_ReturnsOneCandidateListingBothPlatforms()
    {
        factory.SpotifyProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-merge-1", new Uri("https://open.spotify.com/track/sp-merge-1"))]),
        ];
        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Metallica - Nothing Else Matters (Official Music Video)",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.YouTube, "yt-merge-1", new Uri("https://www.youtube.com/watch?v=yt-merge-1"))]),
        ];

        var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = await response.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
        candidates.Should().ContainSingle();
        candidates![0].Name.Should().Be("Nothing Else Matters");
        candidates[0].Platforms.Should().Equal(PlatformCodes.Spotify, PlatformCodes.YouTube);
    }

    [Fact]
    public async Task Search_SameResultFromAllThreeProviders_QobuzWinsTheSpineAndAllThreePlatformsAreListed()
    {
        // No other test in this shared-fixture class ever sets QobuzProvider.Results away from its
        // default empty list, so it must be put back afterward — unlike Spotify/YouTube, nothing
        // else in the class re-zeroes it at the start of its own test.
        factory.QobuzProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.Qobuz, "qb-merge-1", new Uri("https://open.qobuz.com/track/qb-merge-1"))]),
        ];
        factory.SpotifyProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-merge-2", new Uri("https://open.spotify.com/track/sp-merge-2"))]),
        ];
        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Metallica - Nothing Else Matters (Official Music Video)",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.YouTube, "yt-merge-2", new Uri("https://www.youtube.com/watch?v=yt-merge-2"))]),
        ];
        try
        {
            var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var candidates = await response.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
            candidates.Should().ContainSingle();
            candidates![0].Name.Should().Be("Nothing Else Matters");
            candidates[0].Artist.Should().Be("Metallica");
            candidates[0].Platforms.Should().Equal(PlatformCodes.Qobuz, PlatformCodes.Spotify, PlatformCodes.YouTube);
        }
        finally
        {
            factory.QobuzProvider.Results = [];
        }
    }

    [Fact]
    public async Task Search_WhenOneProviderFails_ReturnsTheRemainingProvidersResults()
    {
        factory.SpotifyProvider.ThrowProviderException = true;
        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Still Here",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.YouTube, "still-here", new Uri("https://www.youtube.com/watch?v=still-here"))]),
        ];
        try
        {
            var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var candidates = await response.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
            candidates.Should().ContainSingle();
            candidates![0].Name.Should().Be("Still Here");
        }
        finally
        {
            factory.SpotifyProvider.ThrowProviderException = false;
        }
    }

    [Fact]
    public async Task Search_WhenAllProvidersFail_ReturnsBadRequest()
    {
        SetAllProvidersThrowing(true);
        try
        {
            var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            SetAllProvidersThrowing(false);
        }

        void SetAllProvidersThrowing(bool value)
        {
            foreach (var stub in factory.AllProviders)
            {
                stub.ThrowProviderException = value;
            }
        }
    }

    [Fact]
    public async Task Search_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resolve_WithValidCandidateId_CreatesSharePageWithBothLinks()
    {
        factory.SpotifyProvider.Results = [];
        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                null,
                [
                    new ProviderLinkCandidate(PlatformCodes.YouTube, "resolve-1", new Uri("https://www.youtube.com/watch?v=resolve-1")),
                    new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, "resolve-1", new Uri("https://music.youtube.com/watch?v=resolve-1")),
                ]),
        ];
        var searchResponse = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));
        var candidates = await searchResponse.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();

        var response = await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(candidates![0].Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sharePage = await response.Content.ReadFromJsonAsync<SharePageResponse>();
        sharePage!.Name.Should().Be("Nothing Else Matters");
        sharePage.Artist.Should().Be("Metallica");
        sharePage.Links.Should().HaveCount(2);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.YouTube);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.YouTubeMusic);
        sharePage.Links.Should().OnlyContain(l => l.Type == PlatformType.StreamingService);
    }

    [Fact]
    public async Task Resolve_MergedCandidate_CreatesSharePageWithLinksFromBothProviders()
    {
        factory.SpotifyProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-resolve-1", new Uri("https://open.spotify.com/track/sp-resolve-1"))]),
        ];
        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Metallica - Nothing Else Matters",
                "Metallica",
                null,
                [
                    new ProviderLinkCandidate(PlatformCodes.YouTube, "yt-resolve-1", new Uri("https://www.youtube.com/watch?v=yt-resolve-1")),
                    new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, "yt-resolve-1", new Uri("https://music.youtube.com/watch?v=yt-resolve-1")),
                ]),
        ];
        var searchResponse = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));
        var candidates = await searchResponse.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
        candidates.Should().ContainSingle();

        var response = await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(candidates![0].Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sharePage = await response.Content.ReadFromJsonAsync<SharePageResponse>();
        sharePage!.Links.Should().HaveCount(3);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.Spotify);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.YouTube);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.YouTubeMusic);
    }

    [Fact]
    public async Task Resolve_MergedCandidate_WhoseYouTubeLinkAlreadyExists_StillAddsTheSpotifyLink()
    {
        factory.SpotifyProvider.Results = [];
        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                null,
                [
                    new ProviderLinkCandidate(PlatformCodes.YouTube, "yt-shared", new Uri("https://www.youtube.com/watch?v=yt-shared")),
                    new ProviderLinkCandidate(PlatformCodes.YouTubeMusic, "yt-shared", new Uri("https://music.youtube.com/watch?v=yt-shared")),
                ]),
        ];
        var firstSearch = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));
        var firstCandidates = await firstSearch.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
        await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(firstCandidates![0].Id));

        // A later search finds the same track on Spotify too — the merged candidate carries the
        // already-resolved YouTube link plus a brand-new Spotify one. See ADR 0011.
        factory.SpotifyProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.Spotify, "sp-shared", new Uri("https://open.spotify.com/track/sp-shared"))]),
        ];
        var secondSearch = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", CandidateType.Song));
        var secondCandidates = await secondSearch.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
        secondCandidates.Should().ContainSingle();

        var resolveResponse = await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(secondCandidates![0].Id));

        var sharePage = await resolveResponse.Content.ReadFromJsonAsync<SharePageResponse>();
        sharePage!.Links.Should().HaveCount(3);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.Spotify);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.YouTube);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.YouTubeMusic);
    }

    [Fact]
    public async Task Resolve_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest("unknown-id"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resolve_WithUnknownCandidateId_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest("unknown-id"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_WithEmptySearch_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("", CandidateType.Song));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithoutType_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/catalog/search", new { search = "nothing" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task Search_WithIntegerType_ReturnsBadRequest(int type)
    {
        var response = await _client.PostAsJsonAsync("/api/catalog/search", new { search = "nothing", type });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(CandidateType.Song, StreamingResultType.Track)]
    [InlineData(CandidateType.Album, StreamingResultType.Release)]
    public async Task Search_PassesTheRequestedTypeToProviders(CandidateType requested, StreamingResultType expected)
    {
        factory.SpotifyProvider.Results = [];
        factory.YouTubeProvider.Results = [];

        var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing", requested));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.YouTubeProvider.LastRequestedType.Should().Be(expected);
    }

    [Fact]
    public async Task GetSharePage_WithExistingId_ReturnsSharePage_WithoutAuth()
    {
        factory.SpotifyProvider.Results = [];
        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Master of Puppets",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.YouTube, "get-1", new Uri("https://www.youtube.com/watch?v=get-1"))]),
        ];
        var searchResponse = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("master", CandidateType.Song));
        var candidates = await searchResponse.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
        var resolveResponse = await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(candidates![0].Id));
        var created = await resolveResponse.Content.ReadFromJsonAsync<SharePageResponse>();

        var response = await _anonymousClient.GetAsync($"/api/catalog/sharepages/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sharePage = await response.Content.ReadFromJsonAsync<SharePageResponse>();
        sharePage!.Name.Should().Be("Master of Puppets");
        sharePage.Artist.Should().Be("Metallica");
        sharePage.Links.Should().ContainSingle(l => l.Platform == PlatformCodes.YouTube);
    }

    [Fact]
    public async Task GetSharePage_WithUnknownId_ReturnsNotFound()
    {
        var response = await _anonymousClient.GetAsync($"/api/catalog/sharepages/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resolve_NewItem_ReturnsARowForEveryProviderPlatformStillChecking()
    {
        var candidateId = await SearchOneYouTubeSongAsync("Row Song", "row-1");

        var response = await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(candidateId));

        var sharePage = await response.Content.ReadFromJsonAsync<SharePageResponse>();
        sharePage!.Platforms.Select(p => p.Platform).Should().Equal("discogs", "qobuz", "tidal", "spotify", "youtube");
        sharePage.Platforms.Should().OnlyContain(p => p.State == RowState.Checking && p.Url == null);
        sharePage.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task GetSharePage_AfterTheBackgroundCheck_ShowsEveryRowSettled()
    {
        var candidateId = await SearchOneYouTubeSongAsync("Settled Song", "settled-1");
        var resolved = await (await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(candidateId))).Content.ReadFromJsonAsync<SharePageResponse>();

        var sharePage = await WaitUntilCompleteAsync(resolved!.Id);

        var rows = sharePage.Platforms.ToDictionary(p => p.Platform);
        rows["youtube"].State.Should().Be(RowState.OtherVersion, "the stub cannot look YouTube up again, so the search row's link stays a name match");
        rows["youtube"].Url.Should().Be(new Uri("https://www.youtube.com/watch?v=settled-1"));
        rows["tidal"].State.Should().Be(RowState.NotFound);
        rows["tidal"].Url.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_SameItemTwice_ReturnsTheSameSharePage()
    {
        var candidateId = await SearchOneYouTubeSongAsync("Twice Song", "twice-1");
        var first = await (await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(candidateId))).Content.ReadFromJsonAsync<SharePageResponse>();
        await WaitUntilCompleteAsync(first!.Id);

        var secondCandidateId = await SearchOneYouTubeSongAsync("Twice Song", "twice-1");
        var second = await (await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(secondCandidateId))).Content.ReadFromJsonAsync<SharePageResponse>();

        second!.Id.Should().Be(first.Id);
        second.IsComplete.Should().BeTrue("everything was checked the first time, so nothing is checked again");
        second.Platforms.Should().NotContain(p => p.State == RowState.Checking);
    }

    [Fact]
    public async Task Events_ForAnItemBeingChecked_StreamsEveryRowThenComplete_WithoutAuth()
    {
        var candidateId = await SearchOneYouTubeSongAsync("Stream Song", "stream-1");
        var resolved = await (await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(candidateId))).Content.ReadFromJsonAsync<SharePageResponse>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/catalog/sharepages/{resolved!.Id}/events");
        using var response = await _anonymousClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        var lines = new List<string>();
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            lines.Add(line);
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        lines.Count(line => line == "event: platform").Should().Be(5);
        lines.Should().Contain(line => line.StartsWith("data: ", StringComparison.Ordinal) && line.Contains("\"platform\":\"youtube\"") && line.Contains("\"state\":\"OtherVersion\""));
        lines.Last(line => line.StartsWith("event:", StringComparison.Ordinal)).Should().Be("event: complete");
    }

    [Fact]
    public async Task Events_WithUnknownId_ReturnsNotFound()
    {
        var response = await _anonymousClient.GetAsync($"/api/catalog/sharepages/{Guid.NewGuid()}/events");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<string> SearchOneYouTubeSongAsync(string name, string videoId)
    {
        foreach (var provider in factory.AllProviders)
        {
            provider.Results = [];
        }

        factory.YouTubeProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                name,
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.YouTube, videoId, new Uri($"https://www.youtube.com/watch?v={videoId}"))]),
        ];
        var searchResponse = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest(name, CandidateType.Song));
        var candidates = await searchResponse.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
        return candidates!.Single().Id;
    }

    private async Task<SharePageResponse> WaitUntilCompleteAsync(Guid id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            var sharePage = await _anonymousClient.GetFromJsonAsync<SharePageResponse>($"/api/catalog/sharepages/{id}", timeout.Token);
            if (sharePage!.IsComplete)
            {
                return sharePage;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(CatalogApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        await ConfirmEmailAsync(factory, client);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private static async Task ConfirmEmailAsync(CatalogApiFactory factory, HttpClient client)
    {
        var link = factory.EmailSender.LastConfirmationLink ?? throw new InvalidOperationException("No confirmation link was captured.");
        var response = await client.GetAsync(new Uri(link).PathAndQuery);
        response.EnsureSuccessStatusCode();
    }

    private sealed record AccessTokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);
}
