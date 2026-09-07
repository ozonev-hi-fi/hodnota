namespace Hodnota.Application.Catalog;

public interface ICatalogRepository
{
    Task<SharePageResult> CreateSharePageAsync(StreamingSearchResult result, CancellationToken cancellationToken);
}
