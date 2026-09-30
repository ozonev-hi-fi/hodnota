using System.Net.Http.Headers;

using Google.Apis.Services;
using Google.Apis.YouTube.v3;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Providers.Qobuz;
using Hodnota.Infrastructure.Providers.Spotify;
using Hodnota.Infrastructure.Providers.Tidal;
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

        // Deferred into the factory delegate, not evaluated here directly, for the same reason
        // YouTube's is above: AddCatalog runs before builder.Build(), so a direct read here would
        // miss any configuration source (e.g. a test host's ConfigureAppConfiguration) only
        // applied at Build() time.
        services.AddSingleton(_ =>
        {
            var token = configuration[DiscogsConfiguration.TokenConfigKey];
            return string.IsNullOrEmpty(token)
                ? throw new InvalidOperationException($"Missing required configuration value '{DiscogsConfiguration.TokenConfigKey}'.")
                : new DiscogsCredentials(token);
        });
        services.AddHttpClient(DiscogsConfiguration.ApiHttpClientName, (serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://api.discogs.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Discogs", $"token={serviceProvider.GetRequiredService<DiscogsCredentials>().Token}");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(DiscogsConfiguration.UserAgent);
        });
        services.AddSingleton<DiscogsApiClient>();
        services.AddScoped<IStreamingProvider, DiscogsStreamingProvider>();

        // Fail-fast like Discogs, deferred into the factory for the same reason.
        services.AddSingleton(_ =>
        {
            var clientId = configuration[TidalConfiguration.ClientIdConfigKey];
            var clientSecret = configuration[TidalConfiguration.ClientSecretConfigKey];
            return string.IsNullOrEmpty(clientId)
                ? throw new InvalidOperationException($"Missing required configuration value '{TidalConfiguration.ClientIdConfigKey}'.")
                : string.IsNullOrEmpty(clientSecret)
                    ? throw new InvalidOperationException($"Missing required configuration value '{TidalConfiguration.ClientSecretConfigKey}'.")
                    : new TidalCredentials(clientId, clientSecret, configuration[TidalConfiguration.CountryCodeConfigKey]);
        });
        services.AddHttpClient(TidalConfiguration.AuthHttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://auth.tidal.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient(TidalConfiguration.ApiHttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://openapi.tidal.com/v2/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<TidalAccessTokenProvider>();
        services.AddSingleton<TidalApiClient>();
        services.AddScoped<IStreamingProvider, TidalStreamingProvider>();

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

        var qobuzAppId = configuration[QobuzConfiguration.AppIdConfigKey];
        var qobuzUserToken = configuration[QobuzConfiguration.UserTokenConfigKey];
        if (!string.IsNullOrEmpty(qobuzAppId) && !string.IsNullOrEmpty(qobuzUserToken))
        {
            services.AddHttpClient(QobuzConfiguration.ApiHttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://www.qobuz.com/api.json/0.2/");
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("X-App-Id", qobuzAppId);
                client.DefaultRequestHeaders.Add("X-User-Auth-Token", qobuzUserToken);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(QobuzConfiguration.UserAgent);
            });
            services.AddSingleton<QobuzApiClient>();
            services.AddScoped<IStreamingProvider, QobuzStreamingProvider>();
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
