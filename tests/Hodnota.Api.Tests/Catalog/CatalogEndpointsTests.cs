using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;
using Hodnota.Infrastructure.Catalog;

namespace Hodnota.Api.Tests.Catalog;

public class CatalogEndpointsTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Search_ReturnsCandidatesFromRegisteredProvider()
    {
        factory.StreamingProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Nothing Else Matters",
                "Metallica",
                new Uri("https://example.com/image.jpg"),
                [new ProviderLinkCandidate(PlatformCodes.YouTube, "search-1", new Uri("https://www.youtube.com/watch?v=search-1"))]),
        ];

        var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = await response.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();
        candidates.Should().ContainSingle();
        candidates![0].Name.Should().Be("Nothing Else Matters");
        candidates[0].Artist.Should().Be("Metallica");
        candidates[0].Type.Should().Be(CandidateType.Song);
    }

    [Fact]
    public async Task Resolve_WithValidCandidateId_CreatesSharePageWithBothLinks()
    {
        factory.StreamingProvider.Results =
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
        var searchResponse = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing"));
        var candidates = await searchResponse.Content.ReadFromJsonAsync<List<SearchCandidateResponse>>();

        var response = await _client.PostAsJsonAsync("/api/catalog/resolve", new ResolveRequest(candidates![0].Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sharePage = await response.Content.ReadFromJsonAsync<SharePageResponse>();
        sharePage!.Name.Should().Be("Nothing Else Matters");
        sharePage.Artist.Should().Be("Metallica");
        sharePage.Links.Should().HaveCount(2);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.YouTube);
        sharePage.Links.Should().Contain(l => l.Platform == PlatformCodes.YouTubeMusic);
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
        var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest(""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WhenProviderFails_ReturnsBadGateway()
    {
        factory.StreamingProvider.ThrowProviderException = true;
        try
        {
            var response = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            factory.StreamingProvider.ThrowProviderException = false;
        }
    }
}
