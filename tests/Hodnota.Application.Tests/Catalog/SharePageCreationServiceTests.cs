using AwesomeAssertions;

using Hodnota.Application.Catalog;

using NSubstitute;

namespace Hodnota.Application.Tests.Catalog;

public class SharePageCreationServiceTests
{
    [Fact]
    public async Task ResolveAsync_UnknownCandidateId_ReturnsNull()
    {
        var cache = Substitute.For<ISearchCandidateCache>();
        cache.Get("missing").Returns((StreamingSearchResult?)null);
        var repository = Substitute.For<ICatalogRepository>();
        var service = new SharePageCreationService(cache, repository);

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
        var service = new SharePageCreationService(cache, repository);

        var result = await service.ResolveAsync("candidate-1", CancellationToken.None);

        result.Should().Be(expected);
    }
}
