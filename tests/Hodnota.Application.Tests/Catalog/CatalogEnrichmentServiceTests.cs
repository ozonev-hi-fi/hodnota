using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Domain.Catalog;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hodnota.Application.Tests.Catalog;

public class CatalogEnrichmentServiceTests
{
    private const string Isrc = "USUM71703861";
    private const string Upc = "602537817016";

    private static readonly ProviderLinkCandidate SearchRowTidalLink = Link("tidal", "search-row");

    private static ProviderLinkCandidate Link(string platformCode, string externalId) =>
        new(platformCode, externalId, new Uri($"https://example.com/{platformCode}/{externalId}"));

    private static StreamingSearchResult Result(string name, StreamingResultType type, params ProviderLinkCandidate[] links) =>
        new(type, name, "Artist", null, links);

    private static IStreamingProvider NewProvider(string providerCode, bool supportsLookup = true, params string[] platformCodes)
    {
        var provider = Substitute.For<IStreamingProvider>();
        provider.ProviderCode.Returns(providerCode);
        provider.LinkPlatformCodes.Returns(platformCodes.Length > 0 ? platformCodes : [providerCode]);
        provider.Supports(Arg.Any<StreamingResultType>()).Returns(true);
        provider.SupportsLookup(Arg.Any<StreamingResultType>()).Returns(supportsLookup);
        return provider;
    }

    private static EnrichmentRequest TrackRequest(string? isrc = Isrc, IReadOnlySet<string>? only = null, params ProviderLinkCandidate[] existing) =>
        new(StreamingResultType.Track, "Song", "Artist", isrc, null, existing, only);

    private static EnrichmentRequest AlbumRequest(string? upc = Upc, params ProviderLinkCandidate[] existing) =>
        new(StreamingResultType.Release, "Album", "Artist", null, upc, existing);

    private static async Task<List<ProviderEnrichment>> RunAsync(EnrichmentRequest request, params IStreamingProvider[] providers)
    {
        var service = new CatalogEnrichmentService(providers, NullLogger<CatalogEnrichmentService>.Instance);
        var results = new List<ProviderEnrichment>();
        await foreach (var result in service.EnrichAsync(request, CancellationToken.None))
        {
            results.Add(result);
        }

        return results;
    }

    [Fact]
    public async Task EnrichAsync_LookupFindsTheItem_IsAnExactMatchWithTheLookupLinks()
    {
        var tidal = NewProvider("tidal");
        var found = Link("tidal", "exact");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>())
            .Returns([Result("Song", StreamingResultType.Track, found)]);

        var results = await RunAsync(TrackRequest(existing: SearchRowTidalLink), tidal);

        results.Should().ContainSingle();
        results[0].Outcome.Should().Be(LookupOutcome.ExactMatch);
        results[0].Links.Should().Equal(found);
        results[0].Confirmed.Should().BeTrue("the provider answered just now");
    }

    [Fact]
    public async Task EnrichAsync_LookupFindsSeveralMatches_LinksOfAllOfThemInOrder()
    {
        var discogs = NewProvider("discogs");
        var master = Link("discogs", "master:1");
        var release = Link("discogs", "release:2");
        discogs.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>())
            .Returns([Result("Album", StreamingResultType.Release, master), Result("Album", StreamingResultType.Release, release)]);

        var results = await RunAsync(AlbumRequest(), discogs);

        results.Single().Outcome.Should().Be(LookupOutcome.ExactMatch);
        results.Single().Links.Should().Equal([master, release], "the caller tries them in order and saves the first one not taken elsewhere");
    }

    [Fact]
    public async Task EnrichAsync_TrackLookup_SendsTheNormalizedIsrc()
    {
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);

        await RunAsync(TrackRequest(isrc: "us-um7-17-03861"), tidal);

        await tidal.Received().LookupAsync(
            Arg.Is<StreamingLookupKey>(key => key.Type == StreamingResultType.Track && key.Codes.SequenceEqual(new[] { Isrc })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_ReleaseLookup_SendsTheBarcodeVariants()
    {
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);

        await RunAsync(AlbumRequest(), tidal);

        await tidal.Received().LookupAsync(
            Arg.Is<StreamingLookupKey>(key => key.Type == StreamingResultType.Release && key.Codes.SequenceEqual(new[] { Upc, "0" + Upc })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_LookupFindsNothingButSearchRowHasALink_KeepsItAsANameMatch()
    {
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);

        var results = await RunAsync(TrackRequest(existing: SearchRowTidalLink), tidal);

        results[0].Outcome.Should().Be(LookupOutcome.NameMatch);
        results[0].Links.Should().Equal(SearchRowTidalLink);
        results[0].Confirmed.Should().BeFalse("nothing new was confirmed, the search row's link was only kept");
    }

    [Fact]
    public async Task EnrichAsync_LookupFindsNothingAndNoSearchRowLink_IsNotFound()
    {
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);

        var results = await RunAsync(TrackRequest(), tidal);

        results[0].Outcome.Should().Be(LookupOutcome.NotFound);
        results[0].Links.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_LookupResultOfTheWrongType_IsIgnored()
    {
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>())
            .Returns([Result("Album", StreamingResultType.Release, Link("tidal", "album"))]);

        var results = await RunAsync(TrackRequest(), tidal);

        results[0].Outcome.Should().Be(LookupOutcome.NotFound);
    }

    [Fact]
    public async Task EnrichAsync_ProviderThrows_IsFailedAndOtherProvidersStillFinish()
    {
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new StreamingProviderException("429", new InvalidOperationException()));
        var qobuz = NewProvider("qobuz");
        qobuz.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>())
            .Returns([Result("Song", StreamingResultType.Track, Link("qobuz", "q1"))]);

        var results = await RunAsync(TrackRequest(), tidal, qobuz);

        results.Single(r => r.ProviderCode == "tidal").Outcome.Should().Be(LookupOutcome.Failed);
        results.Single(r => r.ProviderCode == "qobuz").Outcome.Should().Be(LookupOutcome.ExactMatch);
    }

    [Fact]
    public async Task EnrichAsync_ProviderWithoutLookup_IsNeverCalledAndKeepsItsSearchRowLinks()
    {
        var youTube = NewProvider("youtube", supportsLookup: false, "youtube", "youtube-music");
        var row = new[] { Link("youtube", "v1"), Link("youtube-music", "v1") };

        var results = await RunAsync(TrackRequest(existing: row), youTube);

        results[0].Outcome.Should().Be(LookupOutcome.NameMatch);
        results[0].Links.Should().Equal(row);
        results[0].PlatformCodes.Should().Equal("youtube", "youtube-music");
        await youTube.DidNotReceiveWithAnyArgs().LookupAsync(default!, default);
        await youTube.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    [Fact]
    public async Task EnrichAsync_ProviderWithoutLookupAndNoSearchRowLink_IsNotFoundWithoutAnyCall()
    {
        var youTube = NewProvider("youtube", supportsLookup: false);

        var results = await RunAsync(TrackRequest(), youTube);

        results[0].Outcome.Should().Be(LookupOutcome.NotFound);
        await youTube.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    [Fact]
    public async Task EnrichAsync_NoKey_SearchesByNameAndAcceptsOnlyTheSameNormalizedTitle()
    {
        var tidal = NewProvider("tidal");
        var same = Link("tidal", "same");
        tidal.SearchAsync("Artist Song", StreamingResultType.Track, Arg.Any<CancellationToken>())
            .Returns([Result("Song (Live)", StreamingResultType.Track, Link("tidal", "live")), Result("Song", StreamingResultType.Track, same)]);

        var results = await RunAsync(TrackRequest(isrc: null), tidal);

        results[0].Outcome.Should().Be(LookupOutcome.NameMatch);
        results[0].Links.Should().Equal(same);
        await tidal.DidNotReceiveWithAnyArgs().LookupAsync(default!, default);
    }

    [Fact]
    public async Task EnrichAsync_NoKeyAndNoNameMatch_IsNotFound()
    {
        var tidal = NewProvider("tidal");
        tidal.SearchAsync(Arg.Any<string>(), Arg.Any<StreamingResultType>(), Arg.Any<CancellationToken>())
            .Returns([Result("Another Song", StreamingResultType.Track, Link("tidal", "other"))]);

        var results = await RunAsync(TrackRequest(isrc: null), tidal);

        results[0].Outcome.Should().Be(LookupOutcome.NotFound);
    }

    [Fact]
    public async Task EnrichAsync_NoKeyAndSearchRowAlreadyHasALink_DoesNotSearchAgain()
    {
        var tidal = NewProvider("tidal");

        var results = await RunAsync(TrackRequest(isrc: null, existing: SearchRowTidalLink), tidal);

        results[0].Outcome.Should().Be(LookupOutcome.NameMatch);
        await tidal.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    private static IStreamingProvider NewNameLookupProvider(string providerCode, params string[] platformCodes)
    {
        var provider = Substitute.For<IStreamingProvider, IStreamingNameLookup>();
        provider.ProviderCode.Returns(providerCode);
        provider.LinkPlatformCodes.Returns(platformCodes.Length > 0 ? platformCodes : [providerCode]);
        provider.Supports(Arg.Any<StreamingResultType>()).Returns(true);
        provider.SupportsLookup(Arg.Any<StreamingResultType>()).Returns(false);
        return provider;
    }

    [Fact]
    public async Task EnrichAsync_ProviderFoundByNameOnly_IsSearchedByArtistAndTitleEvenWhenTheItemHasAKey()
    {
        var youTube = NewNameLookupProvider("youtube");
        var link = Link("youtube", "PL1");
        ((IStreamingNameLookup)youTube).FindByNameAsync("Artist", "Album", StreamingResultType.Release, Arg.Any<CancellationToken>())
            .Returns([Result("Artist - Album (Full Album)", StreamingResultType.Release, link)]);

        var results = await RunAsync(AlbumRequest(), youTube);

        results.Single().Outcome.Should().Be(LookupOutcome.NameMatch);
        results.Single().Links.Should().Equal(link);
        await youTube.DidNotReceiveWithAnyArgs().LookupAsync(default!, default);
        await youTube.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    [Fact]
    public async Task EnrichAsync_ProviderFoundByNameOnly_IsNotSearchedWhenTheItemAlreadyHasALinkThere()
    {
        var youTube = NewNameLookupProvider("youtube");
        var existing = Link("youtube", "from-search-row");

        var results = await RunAsync(AlbumRequest(existing: existing), youTube);

        results.Single().Outcome.Should().Be(LookupOutcome.NameMatch);
        results.Single().Links.Should().Equal(existing);
        await ((IStreamingNameLookup)youTube).DidNotReceiveWithAnyArgs().FindByNameAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task EnrichAsync_ProviderFoundByNameOnly_NothingMatches_IsNotFound()
    {
        var youTube = NewNameLookupProvider("youtube");
        ((IStreamingNameLookup)youTube).FindByNameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StreamingResultType>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var results = await RunAsync(AlbumRequest(), youTube);

        results.Single().Outcome.Should().Be(LookupOutcome.NotFound);
    }

    [Fact]
    public async Task EnrichAsync_ProviderFoundByNameOnly_SearchFails_IsFailed()
    {
        var youTube = NewNameLookupProvider("youtube");
        ((IStreamingNameLookup)youTube).FindByNameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StreamingResultType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new StreamingProviderException("quota", new InvalidOperationException()));

        var results = await RunAsync(AlbumRequest(), youTube);

        results.Single().Outcome.Should().Be(LookupOutcome.Failed);
    }

    [Fact]
    public async Task EnrichAsync_InvalidBarcode_CountsAsNoKey()
    {
        var tidal = NewProvider("tidal");
        tidal.SearchAsync(Arg.Any<string>(), Arg.Any<StreamingResultType>(), Arg.Any<CancellationToken>()).Returns([]);

        await RunAsync(AlbumRequest(upc: "12345"), tidal);

        await tidal.DidNotReceiveWithAnyArgs().LookupAsync(default!, default);
        await tidal.Received().SearchAsync("Artist Album", StreamingResultType.Release, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_ProviderThatDoesNotSupportTheType_IsSkipped()
    {
        var discogs = NewProvider("discogs");
        discogs.Supports(StreamingResultType.Track).Returns(false);

        var results = await RunAsync(TrackRequest(), discogs);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_OnlyProviders_LimitsTheCheckToThoseProviders()
    {
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);
        var qobuz = NewProvider("qobuz");

        var results = await RunAsync(TrackRequest(only: new HashSet<string> { "tidal" }), tidal, qobuz);

        results.Select(r => r.ProviderCode).Should().Equal("tidal");
        await qobuz.DidNotReceiveWithAnyArgs().LookupAsync(default!, default);
    }

    [Fact]
    public async Task EnrichAsync_CallerStopsReadingEarly_CancelsTheProvidersStillRunning()
    {
        var slowToken = CancellationToken.None;
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            slowToken = call.Arg<CancellationToken>();
            var source = new TaskCompletionSource<IReadOnlyList<StreamingSearchResult>>();
            slowToken.Register(() => source.TrySetCanceled(slowToken));
            return source.Task;
        });
        var qobuz = NewProvider("qobuz");
        qobuz.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);
        var service = new CatalogEnrichmentService([tidal, qobuz], NullLogger<CatalogEnrichmentService>.Instance);

        await using (var enumerator = service.EnrichAsync(TrackRequest(), CancellationToken.None).GetAsyncEnumerator())
        {
            (await enumerator.MoveNextAsync()).Should().BeTrue();
            enumerator.Current.ProviderCode.Should().Be("qobuz");
        }

        slowToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task EnrichAsync_YieldsAProviderAsSoonAsItIsDone()
    {
        var slow = new TaskCompletionSource<IReadOnlyList<StreamingSearchResult>>();
        var tidal = NewProvider("tidal");
        tidal.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns(slow.Task);
        var qobuz = NewProvider("qobuz");
        qobuz.LookupAsync(Arg.Any<StreamingLookupKey>(), Arg.Any<CancellationToken>()).Returns([]);
        var service = new CatalogEnrichmentService([tidal, qobuz], NullLogger<CatalogEnrichmentService>.Instance);

        await using var enumerator = service.EnrichAsync(TrackRequest(), CancellationToken.None).GetAsyncEnumerator();
        (await enumerator.MoveNextAsync()).Should().BeTrue();
        enumerator.Current.ProviderCode.Should().Be("qobuz");

        slow.SetResult([]);
        (await enumerator.MoveNextAsync()).Should().BeTrue();
        enumerator.Current.ProviderCode.Should().Be("tidal");
    }
}
