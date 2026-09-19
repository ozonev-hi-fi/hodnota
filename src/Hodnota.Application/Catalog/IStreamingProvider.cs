namespace Hodnota.Application.Catalog;

public interface IStreamingProvider
{
    string ProviderCode { get; }

    Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
}
