using System.Threading.Channels;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;

using NSubstitute;

namespace Hodnota.Application.Tests.Catalog;

public class SharePageServiceTests
{
    private static readonly Guid PageId = Guid.NewGuid();

    private static readonly Uri QobuzUrl = new("https://open.qobuz.com/track/1");
    private static readonly Uri TidalUrl = new("https://tidal.com/browse/track/2");

    private static StreamingSearchResult CachedResult() => new(
        StreamingResultType.Track,
        "Nothing Else Matters",
        "Metallica",
        null,
        [new ProviderLinkCandidate("qobuz", "1", QobuzUrl)]);

    private static SharePageResult Page(params SharePageLinkResult[] links) =>
        new(PageId, StreamingResultType.Track, "Nothing Else Matters", "Metallica", links);

    private static SharePageLinkResult PageLink(string platformCode, Uri url) => new(platformCode, url, PlatformType.StreamingService);

    private static IStreamingProvider NewProvider(string providerCode, bool supportsTracks = true)
    {
        var provider = Substitute.For<IStreamingProvider>();
        provider.ProviderCode.Returns(providerCode);
        provider.LinkPlatformCodes.Returns([providerCode]);
        provider.Supports(StreamingResultType.Track).Returns(supportsTracks);
        provider.Supports(StreamingResultType.Release).Returns(true);
        return provider;
    }

    private static PlatformCheckResult Check(string platformCode, LookupOutcome outcome, Uri? url = null) => new(platformCode, outcome, url);

    private sealed class Fixture
    {
        public ISearchCandidateCache Cache { get; } = Substitute.For<ISearchCandidateCache>();

        public ICatalogRepository Repository { get; } = Substitute.For<ICatalogRepository>();

        public IEnrichmentQueue Queue { get; } = Substitute.For<IEnrichmentQueue>();

        public TestHub Hub { get; } = new();

        public SharePageService Service { get; }

        public Fixture(SharePageResult page, IReadOnlyList<PlatformCheckResult> checks, params IStreamingProvider[] providers)
        {
            Cache.Get("candidate-1").Returns(CachedResult());
            Repository.ResolveSharePageAsync(Arg.Any<StreamingSearchResult>(), Arg.Any<CancellationToken>()).Returns(page);
            Repository.GetSharePageAsync(page.Id, Arg.Any<CancellationToken>()).Returns(page);
            Repository.GetPlatformChecksAsync(page.Id, Arg.Any<CancellationToken>()).Returns(checks);
            Repository.GetPlatformTypesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(call => call.Arg<IReadOnlyCollection<string>>().ToDictionary(code => code, code => code == "discogs" ? PlatformType.Database : PlatformType.StreamingService));
            Service = new SharePageService(Cache, Repository, providers, Queue, Hub);
        }
    }

    private sealed class TestHub : IShareEventHub
    {
        private readonly Channel<IReadOnlyList<PlatformCheckResult>> _channel = Channel.CreateUnbounded<IReadOnlyList<PlatformCheckResult>>();

        public void Publish(Guid sharePageId, IReadOnlyList<PlatformCheckResult> results) => _channel.Writer.TryWrite(results);

        public IShareEventSubscription Subscribe(Guid sharePageId) => new Subscription(_channel);

        private sealed class Subscription(Channel<IReadOnlyList<PlatformCheckResult>> channel) : IShareEventSubscription
        {
            public IAsyncEnumerable<IReadOnlyList<PlatformCheckResult>> ReadAllAsync(CancellationToken cancellationToken) =>
                channel.Reader.ReadAllAsync(cancellationToken);

            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public async Task ResolveAsync_UnknownCandidateId_ReturnsNullAndTouchesNothing()
    {
        var fixture = new Fixture(Page(), []);
        fixture.Cache.Get("missing").Returns((StreamingSearchResult?)null);

        var result = await fixture.Service.ResolveAsync("missing", CancellationToken.None);

        result.Should().BeNull();
        await fixture.Repository.DidNotReceive().ResolveSharePageAsync(Arg.Any<StreamingSearchResult>(), Arg.Any<CancellationToken>());
        fixture.Queue.DidNotReceiveWithAnyArgs().Enqueue(default, default!);
    }

    [Fact]
    public async Task ResolveAsync_NewItem_QueuesEveryProviderAndShowsEveryRowAsChecking()
    {
        var fixture = new Fixture(Page(PageLink("qobuz", QobuzUrl)), [], NewProvider("tidal"), NewProvider("qobuz"));

        var view = await fixture.Service.ResolveAsync("candidate-1", CancellationToken.None);

        fixture.Queue.Received().Enqueue(PageId, Arg.Is<IReadOnlyCollection<string>>(codes => codes.OrderBy(c => c).SequenceEqual(new[] { "qobuz", "tidal" })));
        view!.Rows.Select(r => (r.PlatformCode, r.State)).Should().Equal(("qobuz", PlatformRowState.Checking), ("tidal", PlatformRowState.Checking));
        view.Rows.Should().OnlyContain(r => r.Url == null, "a link is shown when its check result arrives");
        view.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_ItemChecked_QueuesNothingAndReportsComplete()
    {
        var fixture = new Fixture(
            Page(PageLink("qobuz", QobuzUrl), PageLink("tidal", TidalUrl)),
            [Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl), Check("tidal", LookupOutcome.NotFound)],
            NewProvider("qobuz"),
            NewProvider("tidal"));

        var view = await fixture.Service.ResolveAsync("candidate-1", CancellationToken.None);

        fixture.Queue.DidNotReceiveWithAnyArgs().Enqueue(default, default!);
        view!.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_OnePlatformFailedBefore_QueuesOnlyThatProvider()
    {
        var fixture = new Fixture(
            Page(),
            [Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl), Check("tidal", LookupOutcome.Failed)],
            NewProvider("qobuz"),
            NewProvider("tidal"));

        await fixture.Service.ResolveAsync("candidate-1", CancellationToken.None);

        fixture.Queue.Received().Enqueue(PageId, Arg.Is<IReadOnlyCollection<string>>(codes => codes.SequenceEqual(new[] { "tidal" })));
    }

    [Fact]
    public async Task ResolveAsync_ProviderAddedAfterTheItemWasChecked_QueuesIt()
    {
        var fixture = new Fixture(
            Page(),
            [Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl)],
            NewProvider("qobuz"),
            NewProvider("tidal"));

        await fixture.Service.ResolveAsync("candidate-1", CancellationToken.None);

        fixture.Queue.Received().Enqueue(PageId, Arg.Is<IReadOnlyCollection<string>>(codes => codes.SequenceEqual(new[] { "tidal" })));
    }

    [Fact]
    public async Task ResolveAsync_FailedPlatformBeingCheckedAgain_ShowsAsCheckingSoThePageFollowsTheRetry()
    {
        var fixture = new Fixture(
            Page(),
            [Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl), Check("tidal", LookupOutcome.Failed)],
            NewProvider("qobuz"),
            NewProvider("tidal"));
        fixture.Queue.IsActive(PageId, "tidal").Returns(true);

        var view = await fixture.Service.ResolveAsync("candidate-1", CancellationToken.None);

        view!.Rows.Single(r => r.PlatformCode == "tidal").State.Should().Be(PlatformRowState.Checking);
        view.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task WatchAsync_FailedPlatformBeingCheckedAgain_WaitsForTheRetryResult()
    {
        var fixture = new Fixture(
            Page(),
            [Check("qobuz", LookupOutcome.NotFound), Check("tidal", LookupOutcome.Failed)],
            NewProvider("qobuz"),
            NewProvider("tidal"));
        fixture.Queue.IsActive(PageId, "tidal").Returns(true);
        fixture.Hub.Publish(PageId, [Check("tidal", LookupOutcome.ExactMatch, TidalUrl)]);

        var events = await fixture.Service.WatchAsync(PageId, CancellationToken.None).ToListAsync();

        events.OfType<PlatformRowEvent>().Select(e => (e.Row.PlatformCode, e.Row.State)).Should().Equal(
            ("qobuz", PlatformRowState.NotFound),
            ("tidal", PlatformRowState.Found));
        events[^1].Should().BeOfType<CompleteEvent>();
    }

    [Fact]
    public async Task WatchAsync_ResultThatIsBothInTheSnapshotAndPublished_IsSentOnce()
    {
        var fixture = new Fixture(
            Page(PageLink("qobuz", QobuzUrl)),
            [Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl)],
            NewProvider("qobuz"),
            NewProvider("tidal"));
        fixture.Hub.Publish(PageId, [Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl)]);
        fixture.Hub.Publish(PageId, [Check("tidal", LookupOutcome.NotFound)]);

        var events = await fixture.Service.WatchAsync(PageId, CancellationToken.None).ToListAsync();

        events.OfType<PlatformRowEvent>().Select(e => e.Row.PlatformCode).Should().Equal("qobuz", "tidal");
        events[^1].Should().BeOfType<CompleteEvent>();
    }

    [Fact]
    public async Task GetAsync_MapsEachOutcomeToARowState()
    {
        var other = new Uri("https://tidal.com/browse/track/9");
        var fixture = new Fixture(
            Page(PageLink("qobuz", QobuzUrl), PageLink("tidal", other)),
            [
                Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl),
                Check("tidal", LookupOutcome.NameMatch, other),
                Check("spotify", LookupOutcome.NotFound),
                Check("discogs", LookupOutcome.Failed),
            ],
            NewProvider("discogs"),
            NewProvider("qobuz"),
            NewProvider("tidal"),
            NewProvider("spotify"));

        var view = await fixture.Service.GetAsync(PageId, CancellationToken.None);

        view!.Rows.Select(r => (r.PlatformCode, r.State, r.Url)).Should().Equal(
            ("discogs", PlatformRowState.Failed, null),
            ("qobuz", PlatformRowState.Found, QobuzUrl),
            ("tidal", PlatformRowState.OtherVersion, other),
            ("spotify", PlatformRowState.NotFound, null));
        view.Rows[0].PlatformType.Should().Be(PlatformType.Database);
        view.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_FoundResultWhoseLinkIsHiddenOnThePage_ShowsAsNotFound()
    {
        var fixture = new Fixture(Page(), [Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl)], NewProvider("qobuz"));

        var view = await fixture.Service.GetAsync(PageId, CancellationToken.None);

        view!.Rows.Single().State.Should().Be(PlatformRowState.NotFound);
        view.Rows.Single().Url.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ProviderThatDoesNotSupportTheType_HasNoRow()
    {
        var fixture = new Fixture(Page(), [], NewProvider("discogs", supportsTracks: false), NewProvider("qobuz"));

        var view = await fixture.Service.GetAsync(PageId, CancellationToken.None);

        view!.Rows.Select(r => r.PlatformCode).Should().Equal("qobuz");
    }

    [Fact]
    public async Task GetAsync_StoredLinkOfAProviderThatIsNotRunning_StaysOnThePage()
    {
        var fixture = new Fixture(Page(PageLink("spotify", new Uri("https://open.spotify.com/track/3"))), [], NewProvider("qobuz"));

        var view = await fixture.Service.GetAsync(PageId, CancellationToken.None);

        view!.Rows.Select(r => (r.PlatformCode, r.State)).Should().Equal(("qobuz", PlatformRowState.Checking), ("spotify", PlatformRowState.Found));
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var fixture = new Fixture(Page(), []);
        fixture.Repository.GetSharePageAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SharePageResult?)null);

        (await fixture.Service.GetAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task WatchAsync_UnknownPage_YieldsNothing()
    {
        var fixture = new Fixture(Page(), []);
        fixture.Repository.GetSharePageAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SharePageResult?)null);

        var events = await fixture.Service.WatchAsync(Guid.NewGuid(), CancellationToken.None).ToListAsync();

        events.Should().BeEmpty();
    }

    [Fact]
    public async Task WatchAsync_CompletePage_ReplaysTheRowsThenCompletes()
    {
        var fixture = new Fixture(
            Page(PageLink("qobuz", QobuzUrl)),
            [Check("qobuz", LookupOutcome.ExactMatch, QobuzUrl), Check("tidal", LookupOutcome.NotFound)],
            NewProvider("qobuz"),
            NewProvider("tidal"));

        var events = await fixture.Service.WatchAsync(PageId, CancellationToken.None).ToListAsync();

        events.Should().HaveCount(3);
        events.OfType<PlatformRowEvent>().Select(e => (e.Row.PlatformCode, e.Row.State)).Should().Equal(
            ("qobuz", PlatformRowState.Found),
            ("tidal", PlatformRowState.NotFound));
        events[^1].Should().BeOfType<CompleteEvent>();
        fixture.Queue.DidNotReceiveWithAnyArgs().Enqueue(default, default!);
    }

    [Fact]
    public async Task WatchAsync_IncompletePage_QueuesTheNeverCheckedOnesAndFollowsTheirResults()
    {
        var fixture = new Fixture(
            Page(),
            [Check("qobuz", LookupOutcome.Failed)],
            NewProvider("qobuz"),
            NewProvider("tidal"),
            NewProvider("spotify"));
        fixture.Hub.Publish(PageId, [Check("tidal", LookupOutcome.ExactMatch, TidalUrl)]);
        fixture.Hub.Publish(PageId, [Check("spotify", LookupOutcome.NotFound)]);

        var events = await fixture.Service.WatchAsync(PageId, CancellationToken.None).ToListAsync();

        fixture.Queue.Received().Enqueue(PageId, Arg.Is<IReadOnlyCollection<string>>(codes => codes.OrderBy(c => c).SequenceEqual(new[] { "spotify", "tidal" })));
        events.OfType<PlatformRowEvent>().Select(e => (e.Row.PlatformCode, e.Row.State, e.Row.Url)).Should().Equal(
            ("qobuz", PlatformRowState.Failed, null),
            ("tidal", PlatformRowState.Found, TidalUrl),
            ("spotify", PlatformRowState.NotFound, null));
        events[^1].Should().BeOfType<CompleteEvent>();
    }
}
