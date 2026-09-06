using Hodnota.Application.Catalog;

namespace Hodnota.Api.Tests.Catalog;

// Stands in for YouTubeStreamingProvider in tests — never makes a real HTTP call or needs an API key.
public sealed class StubStreamingProvider : IStreamingProvider
{
    public List<StreamingSearchResult> Results { get; set; } = [];

    public Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StreamingSearchResult>>(Results);
}
