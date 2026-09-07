using Hodnota.Application.Catalog;
using Hodnota.Infrastructure;
using Hodnota.Infrastructure.Providers.YouTube;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hodnota.Api.Tests.Catalog;

// Runs the real API host against an in-memory (shared-cache, so it survives across requests) SQLite
// database, with the real YouTube provider swapped for a stub — no real API key, no network calls.
// Program.cs still resolves a real YouTubeService singleton eagerly at startup though, so a
// placeholder YouTube:ApiKey value is required for the app to boot at all.
public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = $"hodnota-catalog-tests-{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared,
    }.ToString();

    private readonly SqliteConnection _keepAliveConnection;

    public StubStreamingProvider StreamingProvider { get; } = new();

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
            services.AddSingleton<IStreamingProvider>(StreamingProvider);
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
