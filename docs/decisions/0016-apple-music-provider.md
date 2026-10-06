# 0016. Apple Music provider

Status: accepted

## Context

[roadmap.md](../roadmap.md) lists Apple Music as the next first-release provider. Most of what a provider needs is already settled: the `IStreamingProvider` shape and the SDK-vs-`HttpClient` rule ([decisions/0008](0008-youtube-search-sharepage-skeleton.md)), provider identity, trust order, merging, and failure isolation ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)), the required Song/Album search type ([decisions/0012](0012-discogs-provider.md)), and the exact-key lookup every provider needs ([decisions/0014](0014-resolve-time-catalog-enrichment.md)).

Apple Music raises its own questions:

1. Apple has two ways in: the official Apple Music API (JWT-signed, needs a paid Apple Developer Program account), and the free public iTunes Search API. Which one, and what does that decide?
2. Does the free API produce a real, playable Apple Music link, not just an iTunes Store page?
3. Does it carry ISRC/UPC for the exact lookup [decisions/0014](0014-resolve-time-catalog-enrichment.md) expects every provider to try?
4. Its album search misses albums that its own catalog has.
5. Where it sits in `ProviderTrustOrder`.

## Decision

### Access: the free iTunes Search API, behind the ToS-review gate

hodnota calls only `https://itunes.apple.com/search` and needs no app id, no key, no login. The official Apple Music API was rejected: it requires a paid ($99/year) Apple Developer Program membership just to create the MusicKit identifier and private key a developer token is signed with, and the project owner declined that cost the same way they declined to pay for Spotify Premium ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s addendum). Building against the official API would have produced a second dormant provider nobody can test, which the project owner explicitly did not want to repeat.

The iTunes Search API's own terms restrict use to "promot[ing] store content and not for entertainment purposes", and bar using it "to promote any other goods or services". This is a real ToS constraint, not a clean gray area, so Apple Music follows Deezer's and Qobuz's rule: built and used now, but it must not reach real users until [roadmap.md](../roadmap.md)'s third-party ToS-review item is resolved.

### Registration: always on, one optional config value

Following [decisions/0013](0013-tidal-provider.md)'s two-question table:

| Provider | Can any contributor get credentials self-serve? | Do correct credentials work immediately? | Registration |
|---|---|---|---|
| Deezer | no credentials needed | — | always on |
| Apple Music | no credentials needed | — | always on |

Apple Music is registered unconditionally, like Deezer. There is no fail-fast startup check because there is nothing to check. One config value is still read: `AppleMusic:Country` (default `US`), the iTunes storefront. Choosing the storefront from the user's own country (IP-based, or an "I'm from" preference) is out of scope here and is tracked as a [docs/ideas.md](../ideas.md) entry instead.

### Links: real Apple Music links, confirmed live

A live search (2026-10-06) for "metallica enter sandman" returned:

```json
"trackViewUrl": "https://music.apple.com/us/album/enter-sandman/1572051816?i=1572051818&uo=4"
```

`music.apple.com` is Apple Music's own web player and the link produced by its in-app Share action — not an iTunes Store link. There is no separate "lossless" link: lossless is a playback setting on the listener's own Apple Music subscription, not a different URL. The provider strips the `uo` tracking parameter before storing the link.

`isStreamable: false` marks an item that is for sale but not available for on-demand playback in Apple Music (an iTunes-only purchase). These are kept rather than dropped: Apple Music is also a purchase platform, and excluding them would silently drop real catalog entries the user asked to find.

### Search: track is one call; album is two, merged

Confirmed against the live API, 2026-10-06, US storefront.

| Search type | Request |
|---|---|
| Song | `GET search?term={query}&entity=song&media=music&limit=5&country={country}` |
| Album | `GET search?term={query}&entity=album&media=music&limit=5&country={country}` **and** `GET search?term={query}&entity=song&media=music&limit=25&country={country}` |

`entity=album` search is unreliable — it does not reliably return an album by its name even when that album is in the catalog:

| Query | `entity=album` search | via `entity=song`, grouped by album |
|---|---|---|
| metallica load | not found in the first 25 | found (`Load (Remastered)`) |
| jinjer wallflowers | not found | found (`Wallflowers`) |
| nostromo ecce lex | found, #1 | found |
| boards of canada music has the right to children | found, #1 | found |
| okean elzy model | not found (title is Cyrillic, "Модель") | not found |

So an album search makes both calls: album-search results first, then albums built from the song search grouped by `collectionId`, de-duplicated, filtered through `SearchResultRelevanceFilter` (the song-based pass otherwise admits off-topic hits, e.g. an unrelated "Fronté News" single sharing a word with the query), and capped at 5. The transliteration miss (`okean elzy model`) is the same weakness already documented for Deezer in [decisions/0015](0015-deezer-provider.md) — not new to this provider.

Track search is a single call; iTunes track search was found reliable in testing.

A first version capped the merged list at 5 by taking album-search hits first, then song-grouped ones. Live testing (2026-10-06) found this starved the song-grouped supplement whenever `entity=album` alone returned a full page: a "metallica load" search got back 5 *other* Metallica albums from `entity=album` (none of them *Load*), filling the cap before "Load (Remastered)" — found only via the song-grouped pass — was ever considered, defeating the reason that pass exists. The fix: after the existing relevance filter, a result whose own name (not just the shared artist) matches a query word is kept ahead of one that matches only on artist, before the cap is applied.

### Mapping

- Track: name = `trackName` as returned (it already carries the version inline, e.g. "Enter Sandman (Remastered 2021)", the same as Deezer and Tidal's `Title (Version)` rule). Artist = `artistName`, `"Unknown"` when missing. `ExternalId` = `trackId`. Link = `trackViewUrl` with `uo` stripped; if missing, `https://music.apple.com/{country}/song/{trackId}`. No ISRC — track search results do not carry one.
- Album: name = `collectionName` with a trailing ` - Single` or ` - EP` removed (iTunes' own convention, confirmed live: "Kafka - Single", "Micro - EP"). `ReleaseType` is `Single`/`EP` from that suffix, else `Album`. `ExternalId` = `collectionId`. Link = `collectionViewUrl` with its query string removed (a song-derived URL otherwise carries `?i={trackId}`, pointing at one track instead of the album) and `uo` was never there to begin with; fallback `https://music.apple.com/{country}/album/{collectionId}`. No UPC — album search never returns one, and there is no equivalent of Deezer's/Tidal's exact-UPC endpoint (see below).
- Image: `artworkUrl100` with `100x100bb` replaced by `600x600bb` (confirmed live to resolve to a real 600×600 JPEG); the original URL is used unchanged if that substring isn't present.
- A result with no id or a blank name is skipped.

### No exact lookup: the UPC endpoint is unusably fuzzy

`GET lookup?upc={code}` was tested against a real UPC (`602478324055`, Metallica's *Load*) and against a fabricated one (`0000000000000`):

| Query | Result |
|---|---|
| `upc=602478324055` | 3 albums, none of them reporting their own UPC in the response |
| `upc=0000000000000` (fake) | **24 unrelated albums** (e.g. an unrelated Jully Black release) |

No result in any response carries its own `upc` field, so there is no way to confirm "this item's code equals the key's code", which [decisions/0014](0014-resolve-time-catalog-enrichment.md) requires of every lookup. There is also no documented ISRC lookup. `SupportsLookup` therefore returns `false` for both types, and `LookupAsync` throws `NotSupportedException`, exactly like YouTube.

Instead, `AppleMusicStreamingProvider` implements `IStreamingNameLookup`, copying YouTube's pattern: `FindByNameAsync` searches by `"{artist} {name}"` and keeps only results `SearchResultNameMatcher.IsSameItem` accepts. A share-page row for Apple Music can only ever be `NameMatch`, never `ExactMatch`.

### Rate limit: 20 calls/minute, per server IP, shared

Apple's own iTunes Search API page documents "approximately 20 calls per minute (subject to change)". As with Deezer, every hodnota user shares the server's one IP. A track search costs one call; an album search costs two. A rate-limit hit is handled as any other provider failure ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)): logged and dropped for that one search, no retry.

### Trust order: after Deezer, before YouTube

Apple Music's search metadata is clean and structured (distinct artist/title/artwork fields), at the same level as Deezer and Spotify, but it carries neither ISRC nor UPC — strictly weaker on verifiable keys than every provider ranked above it. `ProviderTrustOrder.Order` becomes `discogs, qobuz, tidal, spotify, deezer, apple-music, youtube`.

### Attribution: no credit line

Apple's developer guidelines ask for an Apple Music badge/logo in some contexts, not a written, API-access-conditioned text credit comparable to Tidal's or Discogs's. Following [decisions/0015](0015-deezer-provider.md)'s reasoning for Deezer, no credit line is added for a style guideline that is not a stated condition of access. The badge requirement joins Tidal's and Deezer's logo items in [roadmap.md](../roadmap.md)'s ToS-review item, as an input to the visual-design work, not a per-provider patch.

## Consequences

- A seventh `IStreamingProvider`, and the second with no credentials and no fail-fast startup check (after Deezer). It runs in every environment, including CI test hosts, though the tests never call it.
- `ProviderTrustOrder` gains `apple-music` between `deezer` and `youtube`.
- Apple Music never produces an `ExactMatch` enrichment outcome — only `NameMatch` or `NotFound`/`Failed`. A share page can show an Apple Music link without ever confirming it against an ISRC/UPC.
- Album search costs two calls instead of one, tightening the effective 20-calls/minute budget for album searches specifically.
- The iTunes Search API's own terms ("promote store content... not for entertainment purposes") limit how this can be used once hodnota has real users; this is an explicit new clause on [roadmap.md](../roadmap.md)'s ToS-review item, alongside Apple's badge guideline.
- [roadmap.md](../roadmap.md)'s Apple Music sub-item is checked off, and its ToS-review item gains an Apple Music clause. [architecture.md](../architecture.md)'s streaming-provider integration section gains an Apple Music sentence. [ideas.md](../ideas.md) gains a "storefront per user" entry.
