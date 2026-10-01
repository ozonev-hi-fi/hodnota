namespace Hodnota.Application.Catalog;

// For a provider that has no lookup by ISRC/UPC but can be searched by artist and title, and that
// knows how to tell its own results apart (YouTube). Used when the item has no link there yet.
public interface IStreamingNameLookup
{
    Task<IReadOnlyList<StreamingSearchResult>> FindByNameAsync(string artistName, string name, StreamingResultType type, CancellationToken cancellationToken);
}
