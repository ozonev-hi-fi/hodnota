using Hodnota.Application.Catalog;

namespace Hodnota.Api.Tests.Catalog;

// Stands in for a real IStreamingProvider in tests — never makes a real HTTP call or needs an API key.
public sealed class StubStreamingProvider(string providerCode) : IStreamingProvider
{
    public string ProviderCode { get; } = providerCode;

    public List<StreamingSearchResult> Results { get; set; } = [];

    public bool ThrowProviderException { get; set; }

    public Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
        ThrowProviderException
            ? throw new StreamingProviderException("Simulated provider failure.", new InvalidOperationException())
            : Task.FromResult<IReadOnlyList<StreamingSearchResult>>(Results);
}
