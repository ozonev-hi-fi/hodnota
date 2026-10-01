namespace Hodnota.Application.Catalog;

public static class SearchResultNameMatcher
{
    public static bool IsSameItem(StreamingSearchResult candidate, string artistName, string name)
    {
        var artistKey = SearchResultKey.NormalizeArtist(artistName);
        if (artistKey.Length == 0 || SearchResultKey.NormalizeArtist(candidate.ArtistName) != artistKey)
        {
            return false;
        }

        var wanted = SearchResultKey.Build(new StreamingSearchResult(candidate.Type, name, artistName, null, []));
        return SearchResultKey.Build(candidate with { ArtistName = artistName }) == wanted;
    }
}
