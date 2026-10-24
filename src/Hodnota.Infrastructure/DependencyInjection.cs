using System.Net.Http.Headers;

using Google.Apis.Services;
using Google.Apis.YouTube.v3;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.AppleMusic;
using Hodnota.Infrastructure.Providers.Deezer;
using Hodnota.Infrastructure.Providers.Discogs;
using Hodnota.Infrastructure.Providers.Qobuz;
using Hodnota.Infrastructure.Providers.Spotify;
using Hodnota.Infrastructure.Providers.Tidal;
using Hodnota.Infrastructure.Providers.YouTube;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Quartz;

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

        services.AddAuthorizationBuilder().AddPolicy(AdminConfiguration.RoleName, p => p.RequireRole(AdminConfiguration.RoleName));
        services.AddSingleton<IEmailSender<ApplicationUser>, NoOpEmailSender>();

        return services;
    }

    private static IServiceCollection AddCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton(serviceProvider =>
        {
            var apiKey = configuration[YouTubeConfiguration.ApiKeyConfigKey];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException($"Missing required configuration value '{YouTubeConfiguration.ApiKeyConfigKey}'.");
            }

            var service = new YouTubeService(new BaseClientService.Initializer { ApiKey = apiKey, ApplicationName = "Hodnota" });
            // Unlike every other provider's HttpClient (AddHttpClient's explicit 10s, per ADR 0014),
            // this client library has no timeout configuration of its own — it falls back to
            // HttpClient's ~100s default, which would let one slow YouTube call block the whole
            // multi-provider search far longer than any sibling provider's bound.
            service.HttpClient.Timeout = TimeSpan.FromSeconds(10);
            return service;
        });
        // Its own IMemoryCache instance, not the shared one AddMemoryCache() registers above: search
        // candidates are many, short-lived, and unbounded, so they get their own SizeLimit instead of
        // retrofitting Size onto every other cache entry that shares the default instance.
        services.AddSingleton<ISearchCandidateCache>(_ => new MemorySearchCandidateCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = MemorySearchCandidateCache.MaxEntries })));
        services.AddSingleton<IYouTubeApiClient, YouTubeApiClient>();
        services.AddSingleton<YouTubeChannelResolver>();
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

        services.AddHttpClient(DeezerConfiguration.ApiHttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.deezer.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<DeezerApiClient>();
        services.AddScoped<IStreamingProvider, DeezerStreamingProvider>();

        // No credentials, like Deezer above — only the iTunes storefront to search (ADR 0016).
        services.AddSingleton(_ =>
        {
            var country = configuration[AppleMusicConfiguration.CountryConfigKey];
            return new AppleMusicSettings(string.IsNullOrEmpty(country) ? AppleMusicConfiguration.DefaultCountry : country);
        });
        services.AddHttpClient(AppleMusicConfiguration.ApiHttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://itunes.apple.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<AppleMusicApiClient>();
        services.AddScoped<IStreamingProvider, AppleMusicStreamingProvider>();

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

        services.AddSingleton<InProcessEnrichmentQueue>();
        services.AddSingleton<IEnrichmentQueue>(serviceProvider => serviceProvider.GetRequiredService<InProcessEnrichmentQueue>());
        services.AddSingleton<IShareEventHub, ShareEventHub>();
        services.AddHostedService<EnrichmentWorker>();

        services.AddSingleton<IRefreshSettingsCache, RefreshSettingsCache>();
        AddScheduledRefresh(services, configuration);

        services.AddScoped<ICatalogRepository, EfCatalogRepository>();
        services.AddScoped<CatalogSearchService>();
        services.AddScoped<CatalogEnrichmentService>();
        services.AddScoped<SharePageService>();

        return services;
    }

    // Read here, not in a deferred factory like the provider credentials above: Quartz picks its job
    // store through the registrations AddQuartz makes, so the choice can't wait for Build(). Test hosts
    // therefore set Database:Provider as an environment variable (TestDatabaseProvider), because
    // ConfigureAppConfiguration only applies at Build().
    private static void AddScheduledRefresh(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[DatabaseConfiguration.ProviderConfigKey];
        var connectionString = configuration.GetConnectionString(DatabaseConfiguration.ConnectionStringName);

        services.AddQuartz(q =>
        {
            if (DatabaseConfiguration.IsPostgres(provider) && !string.IsNullOrEmpty(connectionString))
            {
                q.UsePersistentStore(store =>
                {
                    store.UsePostgres(connectionString);
                    store.ConfigureStore(storeOptions =>
                    {
                        storeOptions.TablePrefix = "QRTZ_";
                        storeOptions.StoreJobDataAsStrings = true;
                    });
                    store.UseSystemTextJsonSerializer();
                    store.ProvisionSchema();
                });
            }

            var jobKey = new JobKey(nameof(ScheduledRefreshJob));
            q.AddJob<ScheduledRefreshJob>(opts => opts.WithIdentity(jobKey));
            // A cron time, not "an hour from now": Quartz replaces a stored trigger with this one on
            // every boot, so a start time relative to boot would keep moving with each restart.
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{nameof(ScheduledRefreshJob)}-trigger")
                .WithCronSchedule("0 0 * * * ?"));
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
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
