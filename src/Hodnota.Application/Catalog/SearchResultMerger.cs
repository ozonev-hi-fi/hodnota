namespace Hodnota.Application.Catalog;

// Merges search results from several providers into one row per distinct song/album. See ADR 0011
// for the "spine" model this implements: the highest-trust provider's result that claims a match
// key owns the row (its Name/ArtistName/ImageUrl/ReleaseType are what displays); every later result
// that claims the same key only contributes its own links. The spine is not selected as a separate
// step — it emerges from processing providers in trust order with a first-writer-wins index, so a
// provider returning nothing (or having failed — CatalogSearchService turns a failure into an empty
// list before this ever sees it) is indistinguishable from "this provider had no match" and needs no
// special case.
public static class SearchResultMerger
{
    public static IReadOnlyList<StreamingSearchResult> Merge(IEnumerable<IReadOnlyList<StreamingSearchResult>> resultsInTrustOrder)
    {
        var rows = new List<MergedRow>();
        var index = new Dictionary<SearchResultKey.MatchKey, MergedRow>();

        foreach (var providerResults in resultsInTrustOrder)
        {
            foreach (var result in providerResults)
            {
                var key = SearchResultKey.Build(result);
                if (index.TryGetValue(key, out var row))
                {
                    // Already-claimed key: union links only. A second match candidate is dropped,
                    // not appended as its own row — appending would reintroduce the duplicate this
                    // feature exists to remove.
                    row.AddLinks(result.Links);
                }
                else
                {
                    row = new MergedRow(result);
                    index.Add(key, row);
                    rows.Add(row);
                }
            }
        }

        return [.. rows.Select(row => row.ToResult())];
    }

    private sealed class MergedRow(StreamingSearchResult owner)
    {
        private readonly List<ProviderLinkCandidate> _links = [.. owner.Links];
        private readonly HashSet<string> _platformCodes = [.. owner.Links.Select(link => link.PlatformCode)];

        public void AddLinks(IReadOnlyList<ProviderLinkCandidate> links)
        {
            foreach (var link in links)
            {
                // Dedupe by platform code, first-wins — not by (platform, externalId): the spine
                // provider can return near-duplicates of its own (e.g. a track and its remaster),
                // and deduping only by external id would leave two links for the same platform.
                if (_platformCodes.Add(link.PlatformCode))
                {
                    _links.Add(link);
                }
            }
        }

        public StreamingSearchResult ToResult() => owner with { Links = _links };
    }
}
