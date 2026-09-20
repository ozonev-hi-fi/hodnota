using AwesomeAssertions;

using Hodnota.Application.Catalog;

namespace Hodnota.Application.Tests.Catalog;

public class ProviderTrustOrderTests
{
    [Fact]
    public void Sort_ProvidersRegisteredOutOfOrder_ReturnsThemInTrustOrder()
    {
        var providers = new[] { NewProvider(ProviderCodes.YouTube), NewProvider(ProviderCodes.Spotify) };

        var sorted = ProviderTrustOrder.Sort(providers);

        sorted.Select(p => p.ProviderCode).Should().Equal(ProviderCodes.Spotify, ProviderCodes.YouTube);
    }

    [Fact]
    public void Sort_AllThreeProvidersRegisteredOutOfOrder_ReturnsThemInTrustOrder()
    {
        var providers = new[] { NewProvider(ProviderCodes.YouTube), NewProvider(ProviderCodes.Spotify), NewProvider(ProviderCodes.Qobuz) };

        var sorted = ProviderTrustOrder.Sort(providers);

        sorted.Select(p => p.ProviderCode).Should().Equal(ProviderCodes.Qobuz, ProviderCodes.Spotify, ProviderCodes.YouTube);
    }

    [Fact]
    public void Sort_UnknownProviderCode_IsOrderedLast()
    {
        var providers = new[] { NewProvider("unknown"), NewProvider(ProviderCodes.YouTube), NewProvider(ProviderCodes.Spotify) };

        var sorted = ProviderTrustOrder.Sort(providers);

        sorted.Select(p => p.ProviderCode).Should().Equal(ProviderCodes.Spotify, ProviderCodes.YouTube, "unknown");
    }

    [Fact]
    public void Sort_MultipleUnknownProviderCodes_KeepsTheirRelativeOrder()
    {
        var providers = new[] { NewProvider("first-unknown"), NewProvider("second-unknown") };

        var sorted = ProviderTrustOrder.Sort(providers);

        sorted.Select(p => p.ProviderCode).Should().Equal("first-unknown", "second-unknown");
    }

    private static IStreamingProvider NewProvider(string providerCode) => new StubProvider(providerCode);

    private sealed class StubProvider(string providerCode) : IStreamingProvider
    {
        public string ProviderCode { get; } = providerCode;

        public Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
