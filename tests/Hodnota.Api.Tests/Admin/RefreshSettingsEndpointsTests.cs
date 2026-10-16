using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Hodnota.Contracts.Admin;

using Microsoft.Data.Sqlite;

namespace Hodnota.Api.Tests.Admin;

public class RefreshSettingsEndpointsTests : IAsyncDisposable
{
    private const string Password = "P@ssw0rd!123";

    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = $"hodnota-admin-tests-{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared,
    }.ToString();

    private readonly SqliteConnection _keepAliveConnection;

    public RefreshSettingsEndpointsTests()
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    public async ValueTask DisposeAsync()
    {
        await _keepAliveConnection.DisposeAsync();
    }

    [Fact]
    public async Task Get_AsNonAdmin_ReturnsForbidden()
    {
        await using var factory = new AdminApiFactory(_connectionString, adminEmail: null);
        var client = await RegisterAndLoginAsync(factory, "plain-user@example.com");

        var response = await client.GetAsync("/api/admin/refresh-settings");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AsAnonymous_ReturnsUnauthorized()
    {
        await using var factory = new AdminApiFactory(_connectionString, adminEmail: null);

        var response = await factory.CreateClient().GetAsync("/api/admin/refresh-settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AsAdmin_ReturnsDefaultSettings()
    {
        const string adminEmail = "admin-get@example.com";

        await using (var bootstrap = new AdminApiFactory(_connectionString, adminEmail: null))
        {
            await RegisterAndConfirmAsync(bootstrap, adminEmail);
        }

        await using var adminFactory = new AdminApiFactory(_connectionString, adminEmail);
        var client = await LoginAsync(adminFactory, adminEmail);

        var response = await client.GetAsync("/api/admin/refresh-settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<RefreshSettingsResponse>();
        settings.Should().Be(new RefreshSettingsResponse(7, 90));
    }

    [Fact]
    public async Task Put_AsAdmin_UpdatesSettings_AndSubsequentGetReturnsTheNewValues()
    {
        const string adminEmail = "admin-put@example.com";

        await using (var bootstrap = new AdminApiFactory(_connectionString, adminEmail: null))
        {
            await RegisterAndConfirmAsync(bootstrap, adminEmail);
        }

        await using var adminFactory = new AdminApiFactory(_connectionString, adminEmail);
        var client = await LoginAsync(adminFactory, adminEmail);

        var putResponse = await client.PutAsJsonAsync("/api/admin/refresh-settings", new UpdateRefreshSettingsRequest(3, 30));
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync("/api/admin/refresh-settings");
        var settings = await getResponse.Content.ReadFromJsonAsync<RefreshSettingsResponse>();
        settings.Should().Be(new RefreshSettingsResponse(3, 30));
    }

    [Theory]
    [InlineData(0, 90)]
    [InlineData(7, UpdateRefreshSettingsRequest.MaxIntervalDays + 1)]
    public async Task Put_AsAdmin_OutOfRangeValue_ReturnsBadRequest_AndKeepsTheOldValues(int notFoundDays, int foundDays)
    {
        var adminEmail = $"admin-range-{Guid.NewGuid():N}@example.com";

        await using (var bootstrap = new AdminApiFactory(_connectionString, adminEmail: null))
        {
            await RegisterAndConfirmAsync(bootstrap, adminEmail);
        }

        await using var adminFactory = new AdminApiFactory(_connectionString, adminEmail);
        var client = await LoginAsync(adminFactory, adminEmail);

        var putResponse = await client.PutAsJsonAsync("/api/admin/refresh-settings", new UpdateRefreshSettingsRequest(notFoundDays, foundDays));
        putResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var settings = await (await client.GetAsync("/api/admin/refresh-settings")).Content.ReadFromJsonAsync<RefreshSettingsResponse>();
        settings.Should().Be(new RefreshSettingsResponse(7, 90));
    }

    private static async Task<HttpClient> RegisterAndLoginAsync(AdminApiFactory factory, string email)
    {
        await RegisterAndConfirmAsync(factory, email);
        return await LoginAsync(factory, email);
    }

    private static async Task RegisterAndConfirmAsync(AdminApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        var link = factory.EmailSender.LastConfirmationLink
            ?? throw new InvalidOperationException($"No confirmation link was captured for {email}.");
        var confirmResponse = await client.GetAsync(new Uri(link).PathAndQuery);
        confirmResponse.EnsureSuccessStatusCode();
    }

    private static async Task<HttpClient> LoginAsync(AdminApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private sealed record AccessTokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);
}
