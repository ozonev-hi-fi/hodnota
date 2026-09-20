namespace Hodnota.Application.Catalog;

public static class SearchResultRelevanceFilter
{
    private const int MinimumWordLength = 3;

    public static bool IsRelevant(string query, StreamingSearchResult result) => IsRelevant(ParseQueryWords(query), result);

    public static IReadOnlyList<string> ParseQueryWords(string query) =>
        [.. SearchResultKey.Normalize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= MinimumWordLength)];

    public static bool IsRelevant(IReadOnlyList<string> queryWords, StreamingSearchResult result)
    {
        if (queryWords.Count == 0)
        {
            return true;
        }

        var haystack = $"{SearchResultKey.Normalize(result.Name)} {SearchResultKey.NormalizeArtist(result.ArtistName)}";
        return queryWords.Any(word => haystack.Contains(word, StringComparison.Ordinal));
    }
}
