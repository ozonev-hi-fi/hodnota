using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Hodnota.Api.Tests.Identity;

using Microsoft.AspNetCore.Hosting;

namespace Hodnota.Api.Tests.OpenApi;

public class OpenApiEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task InDevelopment_IsReachable(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task InProduction_IsNotMapped(string path)
    {
        using var productionFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = productionFactory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/register", false)]
    [InlineData("/login", false)]
    [InlineData("/refresh", false)]
    [InlineData("/manage/info", true)]
    public async Task Document_DeclaresBearerRequirement_OnlyForAuthorizedEndpoints(string authRoute, bool requiresAuth)
    {
        using var document = await FetchOpenApiDocumentAsync();

        var path = document.RootElement.GetProperty("paths").GetProperty($"/api/auth{authRoute}");
        var operation = path.EnumerateObject().First().Value;

        operation.TryGetProperty("security", out var security).Should().Be(requiresAuth);
    }

    private async Task<JsonDocument> FetchOpenApiDocumentAsync()
    {
        var response = await _client.GetStreamAsync("/openapi/v1.json");
        return await JsonDocument.ParseAsync(response);
    }
}
