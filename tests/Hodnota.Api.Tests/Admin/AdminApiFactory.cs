using Hodnota.Api.Tests.Identity;
using Hodnota.Infrastructure;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Providers.Tidal;
using Hodnota.Infrastructure.Providers.YouTube;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hodnota.Api.Tests.Admin;

// Two instances of this factory, pointed at the same shared-cache in-memory SQLite connection string,
// simulate "register, then set Admin:Email and restart": the first has no Admin:Email and only
// registers/confirms the account; the second has it set, so its startup seeding finds that user.
public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string? _adminEmail;

    public CapturingEmailSender EmailSender { get; } = new();

    public AdminApiFactory(string connectionString, string? adminEmail)
    {
        TestDatabaseProvider.UseSqlite();
        _connectionString = connectionString;
        _adminEmail = adminEmail;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        List<KeyValuePair<string, string?>> config =
        [
            new(DatabaseConfiguration.ProviderConfigKey, DatabaseConfiguration.SqliteProviderName),
            new($"ConnectionStrings:{DatabaseConfiguration.ConnectionStringName}", _connectionString),
            new(YouTubeConfiguration.ApiKeyConfigKey, "test-key"),
            new(DiscogsConfiguration.TokenConfigKey, "test-token"),
            new(TidalConfiguration.ClientIdConfigKey, "test-client-id"),
            new(TidalConfiguration.ClientSecretConfigKey, "test-client-secret"),
        ];
        if (_adminEmail is not null)
        {
            config.Add(new(AdminConfiguration.EmailConfigKey, _adminEmail));
        }

        builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(config));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender<ApplicationUser>>();
            services.AddSingleton<IEmailSender<ApplicationUser>>(EmailSender);
        });
    }
}
