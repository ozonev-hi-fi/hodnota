using AwesomeAssertions;

using Hodnota.Application.Catalog;

using NSubstitute;

namespace Hodnota.Application.Tests.Catalog;

public class CatalogSearchServiceTests
{
    private static StreamingSearchResult NewResult(string name) => new(
        StreamingResultType.Track,
        name,
        "Artist",
        null,
        [new ProviderLinkCandidate("youtube", "id", new Uri("https://example.com"))]);

    [Fact]
    public async Task SearchAsync_StoresEachResultInCacheAndReturnsItsCandidateId()
    {
        var provider = Substitute.For<IStreamingProvider>();
        var results = new List<StreamingSearchResult> { NewResult("Song 1"), NewResult("Song 2") };
        provider.SearchAsync("nothing", Arg.Any<CancellationToken>()).Returns(results);
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Store(Arg.Any<StreamingSearchResult>()).Returns("id-1", "id-2");
        var service = new CatalogSearchService([provider], cache);

        var candidates = await service.SearchAsync("nothing", CancellationToken.None);

        candidates.Should().HaveCount(2);
        candidates[0].CandidateId.Should().Be("id-1");
        candidates[0].Result.Should().Be(results[0]);
        candidates[1].CandidateId.Should().Be("id-2");
        candidates[1].Result.Should().Be(results[1]);
    }

    [Fact]
    public async Task SearchAsync_MergesResultsFromAllRegisteredProviders()
    {
        var providerA = Substitute.For<IStreamingProvider>();
        providerA.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([NewResult("From A")]);
        var providerB = Substitute.For<IStreamingProvider>();
        providerB.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([NewResult("From B")]);
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Store(Arg.Any<StreamingSearchResult>()).Returns("id");
        var service = new CatalogSearchService([providerA, providerB], cache);

        var candidates = await service.SearchAsync("query", CancellationToken.None);

        candidates.Should().HaveCount(2);
        candidates.Select(c => c.Result.Name).Should().BeEquivalentTo(["From A", "From B"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_BlankQuery_ReturnsEmptyWithoutCallingProviders(string query)
    {
        var provider = Substitute.For<IStreamingProvider>();
        var cache = Substitute.For<ISearchCandidateCache>();
        var service = new CatalogSearchService([provider], cache);

        var candidates = await service.SearchAsync(query, CancellationToken.None);

        candidates.Should().BeEmpty();
        await provider.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
