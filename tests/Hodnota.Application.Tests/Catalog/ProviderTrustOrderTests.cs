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
    public void Sort_AllSevenProvidersRegisteredOutOfOrder_ReturnsThemInTrustOrder()
    {
        var providers = new[]
        {
            NewProvider(ProviderCodes.YouTube),
            NewProvider(ProviderCodes.AppleMusic),
            NewProvider(ProviderCodes.Deezer),
            NewProvider(ProviderCodes.Spotify),
            NewProvider(ProviderCodes.Tidal),
            NewProvider(ProviderCodes.Qobuz),
            NewProvider(ProviderCodes.Discogs),
        };

        var sorted = ProviderTrustOrder.Sort(providers);

        sorted.Select(p => p.ProviderCode).Should().Equal(
            ProviderCodes.Discogs, ProviderCodes.Qobuz, ProviderCodes.Tidal, ProviderCodes.Spotify, ProviderCodes.Deezer, ProviderCodes.AppleMusic, ProviderCodes.YouTube);
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

        public IReadOnlyList<string> LinkPlatformCodes { get; } = [providerCode];

        public bool Supports(StreamingResultType type) => true;

        public bool SupportsLookup(StreamingResultType type) => false;

        public Task<IReadOnlyList<StreamingSearchResult>> LookupAsync(StreamingLookupKey key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
