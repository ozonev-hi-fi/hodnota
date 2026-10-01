using Hodnota.Application.Catalog;

namespace Hodnota.Api.Tests.Catalog;

// Stands in for a real IStreamingProvider in tests — never makes a real HTTP call or needs an API key.
public sealed class StubStreamingProvider(string providerCode) : IStreamingProvider
{
    public string ProviderCode { get; } = providerCode;

    public List<StreamingSearchResult> Results { get; set; } = [];

    public bool ThrowProviderException { get; set; }

    public StreamingResultType? LastRequestedType { get; private set; }

    public IReadOnlyList<string> LinkPlatformCodes { get; } = [providerCode];

    public bool Supports(StreamingResultType type) => true;

    public bool SupportsLookup(StreamingResultType type) => false;

    public Task<IReadOnlyList<StreamingSearchResult>> LookupAsync(StreamingLookupKey key, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken)
    {
        LastRequestedType = type;
        return ThrowProviderException
            ? throw new StreamingProviderException("Simulated provider failure.", new InvalidOperationException())
            : Task.FromResult<IReadOnlyList<StreamingSearchResult>>(Results);
    }
}
