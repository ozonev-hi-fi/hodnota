using Google.Apis.Services;
using Google.Apis.YouTube.v3;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.Spotify;
using Hodnota.Infrastructure.Providers.YouTube;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hodnota.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddDatabase()
            .AddAuth()
            .AddCatalog(configuration);

    private static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TimestampsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var provider = configuration[DatabaseConfiguration.ProviderConfigKey];
            if (string.IsNullOrEmpty(provider))
            {
                throw new InvalidOperationException($"Missing required configuration value '{DatabaseConfiguration.ProviderConfigKey}'.");
            }

            var connectionString = configuration.GetConnectionString(DatabaseConfiguration.ConnectionStringName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Missing required connection string '{DatabaseConfiguration.ConnectionStringName}'.");
            }

            ConfigureProvider(options, provider, connectionString);
            options.AddInterceptors(serviceProvider.GetRequiredService<TimestampsInterceptor>());
        });

        return services;
    }

    private static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services
            .AddIdentityApiEndpoints<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddAuthorization();
        services.AddSingleton<IEmailSender<ApplicationUser>, NoOpEmailSender>();

        return services;
    }

    private static IServiceCollection AddCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton(serviceProvider =>
        {
            var apiKey = configuration[YouTubeConfiguration.ApiKeyConfigKey];

            return string.IsNullOrEmpty(apiKey)
                ? throw new InvalidOperationException($"Missing required configuration value '{YouTubeConfiguration.ApiKeyConfigKey}'.")
                : new YouTubeService(new BaseClientService.Initializer { ApiKey = apiKey, ApplicationName = "Hodnota" });
        });
        services.AddSingleton<ISearchCandidateCache, MemorySearchCandidateCache>();
        services.AddScoped<IStreamingProvider, YouTubeStreamingProvider>();

        // Spotify has required an active Premium subscription on the app-owner's account to use the
        // Web API at all since Feb 2026 (see ADR 0011's addendum) — unlike YouTube's key, that isn't
        // fixable by local configuration, so missing credentials mean "not available", not "misconfigured".
        var spotifyClientId = configuration[SpotifyConfiguration.ClientIdConfigKey];
        var spotifyClientSecret = configuration[SpotifyConfiguration.ClientSecretConfigKey];
        if (!string.IsNullOrEmpty(spotifyClientId) && !string.IsNullOrEmpty(spotifyClientSecret))
        {
            services.AddSingleton(new SpotifyCredentials(spotifyClientId, spotifyClientSecret, configuration[SpotifyConfiguration.MarketConfigKey]));
            services.AddHttpClient(SpotifyConfiguration.AccountsHttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://accounts.spotify.com/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            services.AddHttpClient(SpotifyConfiguration.ApiHttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://api.spotify.com/v1/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            services.AddSingleton<SpotifyAccessTokenProvider>();
            services.AddSingleton<SpotifyApiClient>();
            services.AddScoped<IStreamingProvider, SpotifyStreamingProvider>();
        }

        services.AddScoped<ICatalogRepository, EfCatalogRepository>();
        services.AddScoped<CatalogSearchService>();
        services.AddScoped<SharePageService>();

        return services;
    }

    private static void ConfigureProvider(DbContextOptionsBuilder options, string provider, string connectionString)
    {
        switch (provider)
        {
            case DatabaseConfiguration.PostgresProviderName:
                options.UseNpgsql(connectionString);
                break;
            case DatabaseConfiguration.SqliteProviderName:
                options.UseSqlite(connectionString);
                break;
            default:
                throw new InvalidOperationException($"Unknown '{DatabaseConfiguration.ProviderConfigKey}' value: '{provider}'.");
        }
    }
}
