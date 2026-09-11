using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Contracts.Catalog;
using Hodnota.Infrastructure.Catalog;

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
    public async Task Search_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.PostAsJsonAsync("/api/catalog/search", new SearchRequest("nothing"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        sharePage.Links.Should().OnlyContain(l => l.Type == PlatformType.StreamingService);
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

    [Fact]
    public async Task GetSharePage_WithExistingId_ReturnsSharePage_WithoutAuth()
    {
        factory.StreamingProvider.Results =
        [
            new StreamingSearchResult(
                StreamingResultType.Track,
                "Master of Puppets",
                "Metallica",
                null,
                [new ProviderLinkCandidate(PlatformCodes.YouTube, "get-1", new Uri("https://www.youtube.com/watch?v=get-1"))]),
        ];
        var searchResponse = await _client.PostAsJsonAsync("/api/catalog/search", new SearchRequest("master"));
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
