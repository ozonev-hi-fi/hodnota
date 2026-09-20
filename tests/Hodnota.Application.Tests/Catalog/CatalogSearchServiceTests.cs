using AwesomeAssertions;

using Hodnota.Application.Catalog;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hodnota.Application.Tests.Catalog;

public class CatalogSearchServiceTests
{
    private static StreamingSearchResult NewResult(string name, string platformCode = "youtube") => new(
        StreamingResultType.Track,
        name,
        "Artist",
        null,
        [new ProviderLinkCandidate(platformCode, name, new Uri("https://example.com"))]);

    private static IStreamingProvider NewProvider(string providerCode)
    {
        var provider = Substitute.For<IStreamingProvider>();
        provider.ProviderCode.Returns(providerCode);
        return provider;
    }

    [Fact]
    public async Task SearchAsync_StoresEachMergedResultInCacheAndReturnsItsCandidateId()
    {
        var provider = NewProvider(ProviderCodes.YouTube);
        var results = new List<StreamingSearchResult> { NewResult("Song 1"), NewResult("Song 2") };
        provider.SearchAsync("nothing", Arg.Any<CancellationToken>()).Returns(results);
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Store(Arg.Any<StreamingSearchResult>()).Returns("id-1", "id-2");
        var service = new CatalogSearchService([provider], cache, NullLogger<CatalogSearchService>.Instance);

        var candidates = await service.SearchAsync("nothing", CancellationToken.None);

        candidates.Should().HaveCount(2);
        candidates[0].CandidateId.Should().Be("id-1");
        candidates[0].Result.Should().BeEquivalentTo(results[0]);
        candidates[1].CandidateId.Should().Be("id-2");
        candidates[1].Result.Should().BeEquivalentTo(results[1]);
    }

    [Fact]
    public async Task SearchAsync_ResultsFromDifferentProviders_AreMergedInTrustOrder()
    {
        var youTube = NewProvider(ProviderCodes.YouTube);
        youTube.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([NewResult("From YouTube")]);
        var spotify = NewProvider(ProviderCodes.Spotify);
        spotify.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([NewResult("From Spotify")]);
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Store(Arg.Any<StreamingSearchResult>()).Returns("id");
        // Registered YouTube-first deliberately, so this proves trust-order sorting, not registration order.
        var service = new CatalogSearchService([youTube, spotify], cache, NullLogger<CatalogSearchService>.Instance);

        var candidates = await service.SearchAsync("query", CancellationToken.None);

        candidates.Select(c => c.Result.Name).Should().Equal("From Spotify", "From YouTube");
    }

    [Fact]
    public async Task SearchAsync_MoreThanTwentyMergedResults_ReturnsOnlyTheFirstTwentyAndDoesNotCacheTheRest()
    {
        var results = Enumerable.Range(1, 25).Select(i => NewResult($"Song {i}")).ToList();
        var provider = NewProvider(ProviderCodes.YouTube);
        provider.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(results);
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Store(Arg.Any<StreamingSearchResult>()).Returns(_ => Guid.NewGuid().ToString());
        var service = new CatalogSearchService([provider], cache, NullLogger<CatalogSearchService>.Instance);

        var candidates = await service.SearchAsync("query", CancellationToken.None);

        candidates.Should().HaveCount(20);
        candidates.Select(c => c.Result.Name).Should().Equal(results.Take(20).Select(r => r.Name));
        cache.Received(20).Store(Arg.Any<StreamingSearchResult>());
    }

    [Fact]
    public async Task SearchAsync_OneProviderThrows_StillReturnsTheOtherProvidersResults()
    {
        var failing = NewProvider(ProviderCodes.Spotify);
        failing.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new StreamingProviderException("boom", new InvalidOperationException()));
        var working = NewProvider(ProviderCodes.YouTube);
        working.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([NewResult("Still here")]);
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Store(Arg.Any<StreamingSearchResult>()).Returns("id");
        var service = new CatalogSearchService([failing, working], cache, NullLogger<CatalogSearchService>.Instance);

        var candidates = await service.SearchAsync("query", CancellationToken.None);

        candidates.Should().ContainSingle();
        candidates[0].Result.Name.Should().Be("Still here");
    }

    [Fact]
    public async Task SearchAsync_AllProvidersThrow_ThrowsStreamingProviderException()
    {
        var providerA = NewProvider(ProviderCodes.Spotify);
        providerA.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new StreamingProviderException("boom-a", new InvalidOperationException()));
        var providerB = NewProvider(ProviderCodes.YouTube);
        providerB.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new StreamingProviderException("boom-b", new InvalidOperationException()));
        var cache = Substitute.For<ISearchCandidateCache>();
        var service = new CatalogSearchService([providerA, providerB], cache, NullLogger<CatalogSearchService>.Instance);

        var act = () => service.SearchAsync("query", CancellationToken.None);

        await act.Should().ThrowAsync<StreamingProviderException>();
    }

    [Fact]
    public async Task SearchAsync_NoProvidersRegistered_ReturnsEmptyWithoutThrowing()
    {
        var cache = Substitute.For<ISearchCandidateCache>();
        var service = new CatalogSearchService([], cache, NullLogger<CatalogSearchService>.Instance);

        var candidates = await service.SearchAsync("query", CancellationToken.None);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_CallerCancels_PropagatesOperationCanceledException()
    {
        var provider = NewProvider(ProviderCodes.YouTube);
        provider.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        var cache = Substitute.For<ISearchCandidateCache>();
        var service = new CatalogSearchService([provider], cache, NullLogger<CatalogSearchService>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => service.SearchAsync("query", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_BlankQuery_ReturnsEmptyWithoutCallingProviders(string query)
    {
        var provider = NewProvider(ProviderCodes.YouTube);
        var cache = Substitute.For<ISearchCandidateCache>();
        var service = new CatalogSearchService([provider], cache, NullLogger<CatalogSearchService>.Instance);

        var candidates = await service.SearchAsync(query, CancellationToken.None);

        candidates.Should().BeEmpty();
        await provider.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
