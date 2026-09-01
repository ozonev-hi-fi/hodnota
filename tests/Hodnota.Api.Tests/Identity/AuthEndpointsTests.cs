using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

namespace Hodnota.Api.Tests.Identity;

public class AuthEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string Password = "P@ssw0rd!123";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_WithValidData_Succeeds()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = "weak" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessToken()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = UniqueEmail(), password = "WrongPassword!123" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsNewAccessToken()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens!.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        refreshed.Should().NotBeNull();
        refreshed!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    private sealed record AccessTokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);
}
