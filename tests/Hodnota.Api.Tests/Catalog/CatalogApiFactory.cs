using Hodnota.Api.Tests.Identity;
using Hodnota.Application.Catalog;
using Hodnota.Infrastructure;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.YouTube;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hodnota.Api.Tests.Catalog;

// Runs the real API host against an in-memory (shared-cache, so it survives across requests) SQLite
// database, with the real YouTube, Spotify, and Qobuz providers swapped for stubs — no real API
// keys, no network calls. Program.cs still resolves a real YouTubeService singleton eagerly at
// startup though, so a placeholder YouTube config value is required for the app to boot at all.
// Spotify's and Qobuz's own config is left unset — both are optional (see ADR 0011's addendum), so
// neither real provider is ever registered here; RemoveAll<IStreamingProvider>() + the stub re-add
// below don't depend on either being.
public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = $"hodnota-catalog-tests-{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared,
    }.ToString();

    private readonly SqliteConnection _keepAliveConnection;

    public StubStreamingProvider QobuzProvider { get; } = new(ProviderCodes.Qobuz);

    public StubStreamingProvider SpotifyProvider { get; } = new(ProviderCodes.Spotify);

    public StubStreamingProvider YouTubeProvider { get; } = new(ProviderCodes.YouTube);

    public CapturingEmailSender EmailSender { get; } = new();

    public CatalogApiFactory()
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>(DatabaseConfiguration.ProviderConfigKey, DatabaseConfiguration.SqliteProviderName),
            new KeyValuePair<string, string?>($"ConnectionStrings:{DatabaseConfiguration.ConnectionStringName}", _connectionString),
            new KeyValuePair<string, string?>(YouTubeConfiguration.ApiKeyConfigKey, "test-key"),
        ]));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStreamingProvider>();
            // Registered in reverse trust order deliberately, so endpoint tests that depend on
            // Qobuz-before-Spotify-before-YouTube ordering prove ProviderTrustOrder.Sort is doing
            // the sorting, not an accident of registration order.
            services.AddSingleton<IStreamingProvider>(YouTubeProvider);
            services.AddSingleton<IStreamingProvider>(SpotifyProvider);
            services.AddSingleton<IStreamingProvider>(QobuzProvider);
            services.RemoveAll<IEmailSender<ApplicationUser>>();
            services.AddSingleton<IEmailSender<ApplicationUser>>(EmailSender);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }
}
