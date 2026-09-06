using Google.Apis.Services;
using Google.Apis.YouTube.v3;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure.Catalog;
using Hodnota.Infrastructure.Identity;
using Hodnota.Infrastructure.Providers.YouTube;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hodnota.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) =>
        services
            .AddDatabase()
            .AddAuth()
            .AddCatalog();

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
            .AddIdentityApiEndpoints<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddAuthorization();
        services.AddSingleton<IEmailSender<ApplicationUser>, NoOpEmailSender>();

        return services;
    }

    private static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var apiKey = configuration[YouTubeConfiguration.ApiKeyConfigKey];

            return string.IsNullOrEmpty(apiKey)
                ? throw new InvalidOperationException($"Missing required configuration value '{YouTubeConfiguration.ApiKeyConfigKey}'.")
                : new YouTubeService(new BaseClientService.Initializer { ApiKey = apiKey, ApplicationName = "Hodnota" });
        });
        services.AddSingleton<ISearchCandidateCache, MemorySearchCandidateCache>();
        services.AddScoped<IStreamingProvider, YouTubeStreamingProvider>();
        services.AddScoped<ICatalogRepository, EfCatalogRepository>();
        services.AddScoped<CatalogSearchService>();
        services.AddScoped<SharePageCreationService>();

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
