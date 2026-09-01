using Hodnota.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Hodnota.Api.Tests.Identity;

// Runs the real API host against an in-memory (shared-cache, so it survives across requests) SQLite database.
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = $"hodnota-auth-tests-{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared,
    }.ToString();

    private readonly SqliteConnection _keepAliveConnection;

    public AuthApiFactory()
    {
        // Sync is fine: in-memory SQLite has no real I/O, and constructors can't be async anyway.
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>(DatabaseConfiguration.ProviderConfigKey, DatabaseConfiguration.SqliteProviderName),
            new KeyValuePair<string, string?>($"ConnectionStrings:{DatabaseConfiguration.ConnectionStringName}", _connectionString),
        ]));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }
}
