using Hodnota.Api.Tests.Identity;
using Hodnota.Application.Catalog;
using Hodnota.Infrastructure;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Providers.Tidal;
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
// database, with the real YouTube, Spotify, Qobuz, Discogs, Tidal, and Deezer providers swapped for
// stubs — no real API keys, no network calls. Program.cs still resolves a real YouTubeService
// singleton eagerly at startup though, and checks Discogs:Token and Tidal:ClientId/ClientSecret
// there too, so placeholder YouTube, Discogs, and Tidal config values are required for the app to
// boot at all. Spotify's and Qobuz's own config is left unset — both are optional (see ADR 0011's
// addendum), so neither real provider is ever registered here; RemoveAll<IStreamingProvider>() +
// the stub re-add below don't depend on either being. Deezer needs no config at all, so the real
// DeezerStreamingProvider is always registered first, then removed the same way.
public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = $"hodnota-catalog-tests-{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared,
    }.ToString();

    private readonly SqliteConnection _keepAliveConnection;

    public StubStreamingProvider DiscogsProvider { get; } = new(ProviderCodes.Discogs);

    public StubStreamingProvider QobuzProvider { get; } = new(ProviderCodes.Qobuz);

    public StubStreamingProvider SpotifyProvider { get; } = new(ProviderCodes.Spotify);

    public StubStreamingProvider TidalProvider { get; } = new(ProviderCodes.Tidal);

    public StubStreamingProvider DeezerProvider { get; } = new(ProviderCodes.Deezer);

    public StubStreamingProvider YouTubeProvider { get; } = new(ProviderCodes.YouTube);

    // Highest trust first. The only list of stubs: ConfigureServices registers exactly these, and
    // CatalogApiFactoryTests checks it covers every ProviderCodes constant.
    public IReadOnlyList<StubStreamingProvider> AllProviders => [DiscogsProvider, QobuzProvider, TidalProvider, SpotifyProvider, DeezerProvider, YouTubeProvider];

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
            new KeyValuePair<string, string?>(DiscogsConfiguration.TokenConfigKey, "test-token"),
            new KeyValuePair<string, string?>(TidalConfiguration.ClientIdConfigKey, "test-client-id"),
            new KeyValuePair<string, string?>(TidalConfiguration.ClientSecretConfigKey, "test-client-secret"),
        ]));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStreamingProvider>();
            // Registered in reverse trust order deliberately, so endpoint tests that depend on
            // trust ordering prove ProviderTrustOrder.Sort is doing the sorting, not an accident
            // of registration order.
            foreach (var stub in AllProviders.Reverse())
            {
                services.AddSingleton<IStreamingProvider>(stub);
            }

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
