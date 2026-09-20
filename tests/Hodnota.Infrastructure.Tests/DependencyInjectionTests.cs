using AwesomeAssertions;

using Hodnota.Application.Catalog;
using Hodnota.Infrastructure;
using Hodnota.Infrastructure.Providers.Qobuz;
using Hodnota.Infrastructure.Providers.Spotify;
using Hodnota.Infrastructure.Providers.YouTube;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hodnota.Infrastructure.Tests;

// Spotify has been unable to satisfy its own Client Credentials flow without an active Premium
// subscription on the app-owner's account since Feb 2026 (see ADR 0011's addendum) — this asserts
// the resulting "optional provider" behavior in code, not just in a comment.
public class DependencyInjectionTests
{
    private static IConfiguration BuildConfiguration(IDictionary<string, string?> overrides) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DatabaseConfiguration.ProviderConfigKey] = DatabaseConfiguration.SqliteProviderName,
                [$"ConnectionStrings:{DatabaseConfiguration.ConnectionStringName}"] = "DataSource=:memory:",
                [YouTubeConfiguration.ApiKeyConfigKey] = "test-key",
            })
            .AddInMemoryCollection(overrides)
            .Build();

    [Fact]
    public void AddInfrastructure_WithoutSpotifyCredentials_RegistersOnlyYouTubeProvider()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var services = new ServiceCollection().AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var streamingProviders = provider.GetServices<IStreamingProvider>();

        streamingProviders.Select(p => p.ProviderCode).Should().BeEquivalentTo([ProviderCodes.YouTube]);
    }

    [Fact]
    public void AddInfrastructure_WithSpotifyCredentials_RegistersBothProviders()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [SpotifyConfiguration.ClientIdConfigKey] = "client-id",
            [SpotifyConfiguration.ClientSecretConfigKey] = "client-secret",
        });
        var services = new ServiceCollection().AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var streamingProviders = provider.GetServices<IStreamingProvider>();

        streamingProviders.Select(p => p.ProviderCode).Should().BeEquivalentTo([ProviderCodes.YouTube, ProviderCodes.Spotify]);
    }

    [Fact]
    public void AddInfrastructure_WithoutQobuzCredentials_DoesNotRegisterQobuzProvider()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var services = new ServiceCollection().AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var streamingProviders = provider.GetServices<IStreamingProvider>();

        streamingProviders.Select(p => p.ProviderCode).Should().NotContain(ProviderCodes.Qobuz);
    }

    [Fact]
    public void AddInfrastructure_WithQobuzCredentials_RegistersQobuzProvider()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [QobuzConfiguration.AppIdConfigKey] = "app-id",
            [QobuzConfiguration.UserTokenConfigKey] = "user-token",
        });
        var services = new ServiceCollection().AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var streamingProviders = provider.GetServices<IStreamingProvider>();

        streamingProviders.Select(p => p.ProviderCode).Should().BeEquivalentTo([ProviderCodes.YouTube, ProviderCodes.Qobuz]);
    }

    [Fact]
    public void AddInfrastructure_WithOnlyQobuzAppId_DoesNotRegisterQobuzProvider()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [QobuzConfiguration.AppIdConfigKey] = "app-id",
        });
        var services = new ServiceCollection().AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var streamingProviders = provider.GetServices<IStreamingProvider>();

        streamingProviders.Select(p => p.ProviderCode).Should().NotContain(ProviderCodes.Qobuz);
    }
}
