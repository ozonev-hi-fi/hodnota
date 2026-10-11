namespace Hodnota.Application.Catalog;

// For a provider that has no lookup by ISRC/UPC but can be searched by artist and title, and that
// knows how to tell its own results apart (YouTube). Used when the item has no link there yet.
// knownTrackTitles is the catalog's own track titles for a Release request (empty for a Track
// request) — YouTube's channel disambiguation needs them (ADR 0018 D2/E10); other implementers can
// ignore the parameter.
public interface IStreamingNameLookup
{
    Task<IReadOnlyList<StreamingSearchResult>> FindByNameAsync(string artistName, string name, StreamingResultType type, IReadOnlyList<string> knownTrackTitles, CancellationToken cancellationToken);
}
