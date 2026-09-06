namespace Hodnota.Application.Catalog;

public interface IStreamingProvider
{
    Task<IReadOnlyList<StreamingSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
}
