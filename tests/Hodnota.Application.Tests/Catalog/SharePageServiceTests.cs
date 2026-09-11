using AwesomeAssertions;

using Hodnota.Application.Catalog;

using NSubstitute;

namespace Hodnota.Application.Tests.Catalog;

public class SharePageServiceTests
{
    [Fact]
    public async Task ResolveAsync_UnknownCandidateId_ReturnsNull()
    {
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Get("missing").Returns((StreamingSearchResult?)null);
        var repository = Substitute.For<ICatalogRepository>();
        var service = new SharePageService(cache, repository);

        var result = await service.ResolveAsync("missing", CancellationToken.None);

        result.Should().BeNull();
        await repository.DidNotReceive().CreateSharePageAsync(Arg.Any<StreamingSearchResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_KnownCandidateId_CreatesSharePageFromCachedResult()
    {
        var cachedResult = new StreamingSearchResult(
            StreamingResultType.Track,
            "Nothing Else Matters",
            "Metallica",
            null,
            [new ProviderLinkCandidate("youtube", "id", new Uri("https://example.com"))]);
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Get("candidate-1").Returns(cachedResult);
        var expected = new SharePageResult(Guid.NewGuid(), StreamingResultType.Track, "Nothing Else Matters", "Metallica", []);
        var repository = Substitute.For<ICatalogRepository>();
        repository.CreateSharePageAsync(cachedResult, Arg.Any<CancellationToken>()).Returns(expected);
        var service = new SharePageService(cache, repository);

        var result = await service.ResolveAsync("candidate-1", CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        var expected = new SharePageResult(id, StreamingResultType.Track, "Nothing Else Matters", "Metallica", []);
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetSharePageAsync(id, Arg.Any<CancellationToken>()).Returns(expected);
        var service = new SharePageService(Substitute.For<ISearchCandidateCache>(), repository);

        var result = await service.GetAsync(id, CancellationToken.None);

        result.Should().Be(expected);
    }
}
