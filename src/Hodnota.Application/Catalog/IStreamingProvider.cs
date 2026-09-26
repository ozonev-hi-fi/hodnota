namespace Hodnota.Application.Catalog;

public interface IStreamingProvider
{
    string ProviderCode { get; }

    bool Supports(StreamingResultType type);

    Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, StreamingResultType type, CancellationToken cancellationToken);
}
