namespace Hodnota.Application.Catalog;

// Decides whether a result from a service where anyone can upload (YouTube) is the item with this
// artist and title. A title proves nothing: anyone can name a pirate upload "Artist - Album (Full
// Album)". So the uploader has to be the artist: the channel is the artist's own or its auto-generated
// "<Artist> - Topic" channel. The title must then equal the item's title once noise is removed.
// Everything else is not accepted: no link is better than a pirate copy.
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
