# 0014. Resolve-time catalog enrichment

Status: accepted

## Context

[roadmap.md](../roadmap.md) schedules resolve-time enrichment before Deezer, Apple Music, and Bandcamp, so that those providers are built with lookup support from the start. [decisions/0012](0012-discogs-provider.md)'s "Out of scope" section describes the direction: search only helps the user find an item; after the user picks a result, every provider is asked again for that exact item, and the result is one verified catalog entry that later visits reuse.

Today `POST /api/catalog/resolve` makes no provider calls. `SharePageService` takes the cached search row and `EfCatalogRepository.CreateSharePageAsync` saves it as it is. This has five problems:

1. **Links are not verified.** A share page only has links from providers whose result matched the same normalized artist + title (`SearchResultKey`) during that one search. Nobody checked that the links point to the same recording or release.
2. **ISRC/UPC are stored but never used for matching.** Qobuz, Tidal, and Spotify fill them ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md), [decisions/0013](0013-tidal-provider.md)), but no code compares them.
3. **The merged row loses most keys.** `SearchResultMerger` keeps only the first (highest-trust) result's `Isrc`/`Upc`. Discogs ranks first for albums and never sets `Upc` ([decisions/0012](0012-discogs-provider.md)), so most merged album rows have no UPC at all.
4. **Every click creates a new share page**, even for the same item. The same album gets a new URL on each click.
5. **An ISRC/UPC clash creates a duplicate.** When a new item's ISRC or UPC is already stored, `CreateSharePageAsync` retries with the key removed and creates a second entity, instead of reusing the first one.

The roadmap also listed open questions: how long a click may wait, what to store when a provider finds nothing, and rate limits (Discogs: 60 requests per minute).

## Decision

### The click opens the page at once; enrichment runs in the background

`POST /api/catalog/resolve` creates (or finds, see below) the share page and returns at once. It does not wait for providers. The page opens at once; its rows show as checking until each provider's result arrives. A background job then asks each provider for the exact item, and the page receives each result live.

**Rejected alternative:** waiting for all providers inside the click, with a spinner on the search page. A click would take as long as the slowest provider (up to the 10 s HTTP timeout), and the work would be tied to one HTTP request: a friend who opens the same link meanwhile would see nothing change.

### Live updates: Server-Sent Events

The share page subscribes to a one-way event stream (`text/event-stream`) for that page. The server sends one event per platform result and a final event when the check is complete. ASP.NET Core 10 has built-in SSE results, and the browser's built-in `EventSource` reads them, so no new package is needed on either side. `EventSource` cannot send an `Authorization` header; that is not a problem, because share pages are already anonymous ([architecture.md](../architecture.md)).

Every viewer of the page gets the stream, not only the user who clicked. A page opened after the check is complete gets the final state from `GET /api/catalog/sharepages/{id}` and needs no stream.

**Rejected alternatives:**

| Option | Why not |
|---|---|
| SignalR (WebSockets) | Two-way, but only server-to-browser is needed. Adds `@microsoft/signalr` and a hub. |
| Polling | Simplest, but wastes requests and adds delay between a result and the page. |
| Streaming the resolve response (`IAsyncEnumerable`) | Ties the work to the clicking user's request; closing the tab stops the check, and other viewers get no updates. |

### Background runner: an in-process queue

`InProcessEnrichmentQueue` (a `Channel<T>`) feeds a hosted `EnrichmentWorker` that runs up to four jobs at a time. A provider that is already queued or running for a page is not queued twice, so two quick clicks on the same item cost one check.

The queue is not persistent. A restart drops the queued jobs, but nothing is lost that cannot be rebuilt: what was never checked has no `ProviderCheck` row, so the next resolve of the item queues it again, and so does anyone who watches the page (see below).

**Rejected alternative:** a persistent job library (Hangfire or Quartz.NET). Jobs would survive a restart, but it adds tables and setup for a state that the check rows already give. The future scheduled refresh (see "Out of scope") needs a scheduler; that is the time to revisit this.

### Watching a page

`GET /api/catalog/sharepages/{id}/events` first replays every row that is already settled, then follows the running checks, then sends `complete`. It subscribes before it reads the saved state, so a result saved in between is not lost. It also queues the platforms that were never checked (not the failed ones), so a page that a restart interrupted, or one saved before enrichment existed, fills in while someone looks at it. The stream ends after 60 seconds at the latest; the web page then stops the spinners of the rows still open and shows them as "couldn't check right now".

### The share page: one row per platform, each with a state

The page shows a row for every platform covered by a **running** provider that **supports the item's type**, grouped in the existing Listen / Buy / Discover sections. A song page has no Discogs row; there is no Spotify row while Spotify is dormant; a Deezer row appears when a Deezer provider is registered. A seeded platform with no provider (Apple Music, Bandcamp) has no row.

Each row is in one of these states:

| State | Shown as |
|---|---|
| Checking | the platform name and a spinner |
| Found (exact key) | the platform name and the link |
| Found (name match only) | the platform name, the link, and "(other version)" |
| Not found | the platform name and "not found" |
| Failed (timeout, `429`, error) | the platform name and "couldn't check right now" |

The link is shown as its URL, next to the platform name, instead of the platform name being the link text.

The contractual Discogs and Tidal credit lines ([decisions/0012](0012-discogs-provider.md), [decisions/0013](0013-tidal-provider.md)) stay, shown when the page has a Discogs or Tidal link.

### Matching: exact key first, name match as a fallback

**The exact key** is the ISRC for a track and the UPC/EAN barcode for a release. A merged search row takes its ISRC and UPC from the highest-ranked result that has one, not only from the first result. An album whose first result is Discogs (no UPC) can then still use Qobuz's or Tidal's UPC. When providers disagree, the key from the highest-ranked provider in `ProviderTrustOrder` wins.

**Barcodes are normalized.** Providers write the same barcode with different leading zeros (Tidal returns 14 digits, `00602577891953`; Qobuz and Discogs use 12 or 13). The catalog stores one canonical form, 12 digits (UPC-A) when the number fits, else 13 (EAN-13), and a lookup tries the forms one after the other until one matches. No provider normalizes barcodes itself.

**Every result is checked.** An exact lookup can return several items (re-releases can share a code), so a provider returns only an item whose own ISRC/UPC equals the key. Two providers cannot fully do that: a Discogs release lists its barcodes as user-typed text, which the provider normalizes and compares; a Spotify search result holds a simplified album with no UPC, so only the `upc:` filter vouches for it.

**The name match stays as a fallback.** When a provider has no item with the exact key, the link from the search row (same normalized artist + title, [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)) is kept and shown as "(other version)". A remaster or reissue is more useful than no link. [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s normalizer already refuses to merge live, remix, acoustic, demo, and cover versions, which limits the risk.

**When no provider has a key** (for example a YouTube-only row), each provider is searched by name, and only a result with the same normalized key is accepted, as a name match.

`ProviderLink.Confidence` records the difference: `1.0` for an exact-key match and `0.5` for a name match. The database allows one link per platform per entity, so an exact-key match **replaces** a name-match link for the same platform. A lookup can return several matching items (re-releases can share a code, or a Discogs master and its release); the provider lists them in preference order, and the first one not already linked to another catalog entity is saved — only when every one of them is taken does the platform stay unmatched. Several links per platform (original, remaster, …) are deferred, see "Out of scope".

### Provider lookup support

Each provider gets a lookup method next to its search method. Confirmed against each vendor's documentation (and, for Deezer, the live API) on 2026-09-30:

| Provider | Track (ISRC) | Release (UPC) |
|---|---|---|
| Tidal | `GET /tracks?filter[isrc]=` | `GET /albums?filter[barcodeId]=` |
| Discogs | not supported | `GET /database/search?barcode=&type=release`; the result carries `master_id`, so the link goes to the master when there is one, otherwise to the release (the same `master:`/`release:` ids as [decisions/0012](0012-discogs-provider.md)) |
| Spotify | `GET /search?q=isrc:` | `GET /search?q=upc:`, known to miss existing albums |
| Qobuz | no lookup endpoint; a search with the code as the query, then an exact-code filter | the same |
| YouTube | not supported; found by artist and title instead, see "YouTube" below | the same |

Status after the first live check (2026-09-30, real providers, a song and an album):

- **Tidal** works: both the ISRC and the UPC lookup returned the exact item. A probe on 2026-10-01 showed that it needs no country code and finds an album by its 12, 13 and 14 digit barcode alike, so it is asked once. One ISRC can return several tracks (seven for a popular song), which is why the provider keeps only an item whose own code matches.
- **Discogs** works (a probe on 2026-10-01): it found *Nevermind* by its barcode, in the 12 and in the 13 digit form alike, with the release's `master_id` and a `barcode` field. For three other albums it found no release with the barcode Tidal gave, and a plain text search of the number found nothing either, so Discogs does not have those pressings and the row keeps the name match. Because the forms behave the same, it is asked once. Coverage depends on the pressing: a barcode from another edition finds nothing.
- **Spotify** could not be checked: it answered `403` (no Premium subscription, [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)), so its row showed "couldn't check right now".
- **Qobuz** works through its search (a probe on 2026-10-01): the ISRC, or the barcode in the 13 digit form Qobuz stores, used as the query text finds the item, and the provider then checks the code on each result. A 12 digit barcode finds nothing, and `album/get` with a barcode answers 404, so the 13 digit form is asked first.

### YouTube: one search by name, and only when the item has no YouTube link

YouTube has no ISRC/UPC lookup, its album playlists are free-text titles from any uploader, and `search.list` is limited to 100 calls per day per project. The first version of this decision made no YouTube call at resolve time. A live search for "metalica load" showed the cost: YouTube returned only fan uploads, whose "artist" is the uploader's channel, so none merged with the album found on Qobuz and Tidal, and the page had no YouTube link at all.

Enrichment now searches YouTube once for an item that has no YouTube link yet. The query is the item's own artist and title from the catalog, not the user's typed text. An item that the search row already linked costs no call. A result is accepted only when its **uploader is the artist** (the channel is the artist's own, or its automatic "Artist - Topic" channel) and its title, after noise like "(Full Album)" is removed, equals the item's title (`SearchResultNameMatcher`, the same noise rules as [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)). The first accepted result is kept, as a name match, for both the YouTube and the YouTube Music row. Each such search costs one of the 100 daily calls.

The first version of this rule trusted the title: a result was accepted when its title contained the artist and the album. A live resolve of *Ecce Lex* by Nostromo then linked a pirate upload with a perfect title. A title proves nothing, so the rule now trusts the uploader. The cost is that most YouTube rows will say "not found" until the real fix. The investigation and the fix plan are in [youtube-music-search.md](../youtube-music-search.md).

**Not solved:** the official YouTube Music album playlists (ids that start with `OLAK5uy_`). Their channel is "YouTube", their title is "Album - Load", and the artist is nowhere in their metadata. A search for "Metallica Load" returned none of them (checked 2026-10-01). Finding them needs another route; verifying the artist would take one more call per candidate (`playlistItems.list`, whose items name the "Artist - Topic" channel).

### One share page per item

A catalog track or release has one share page, and everyone who picks that item gets the same URL. `SharePage.UserId` stays empty.

Resolve finds the existing item before any provider call: by a stored `ProviderLink (PlatformId, ExternalId)` from the search row (as today), or by the same ISRC (track) or UPC (release). This also replaces today's clash retry that drops the key and creates a duplicate entity.

A click on an existing item returns its share page and makes **no provider calls**, with one exception: platforms whose last check **failed**, or that were **never checked**, are checked again. A short outage or a `429` must not become a permanent "not found". "Never checked" covers an enrichment that a restart interrupted, items saved before this change, and items that predate a newly added provider. Each of those costs one check on the next click, not one per click.

Share pages created before this change stay valid (their URLs are already shared). When an item has several old pages, resolve returns the oldest one. No unique index is added for that reason. Two first clicks at the same moment are already covered: the entity, its links, and its page are saved together, and the existing `(PlatformId, ExternalId)` unique index plus retry makes the second click find the first click's item and page.

### Check results are saved

Each provider check is saved per entity and platform: when it ran and whether it found an exact match, a name match, nothing, or failed. `ProviderLink.LastVerifiedUtc` is set only when the provider returned that link in this check, not when a name match from the search row is merely kept. This costs one table, and the future scheduled refresh needs these dates to decide what to check again, including "entities never checked on a newly added provider".

### Metadata authority stays as it is

The entity's title and artist spelling still come from the clicked row, which follows `ProviderTrustOrder` (`discogs, qobuz, tidal, spotify, youtube`). Splitting it into a catalog-authority order and a display order is deferred.

### Failures and rate limits: no retry inside a check

The 10 s HTTP timeout and the "log and drop, no retry on `429`" rule from [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md) stay. The difference from search is that a failure is now visible and saved as "failed", not the same as "no results", which is what allows the retry on the next click. One new item costs at most a few calls per provider; Discogs's 60 requests per minute and Tidal's per-client limit are shared by all users of one token.

### Out of scope

Named follow-up work, each for its own ADR or roadmap item:

- **Scheduled refresh.** A background job that re-checks existing links from time to time (is the item still on the service?) and re-checks "not found" platforms more often (has it appeared?). It uses the saved check dates.
- **User-suggested links.** A viewer suggests a link instead of, or in addition to, an existing one; hodnota checks that the link is valid and fits the page.
- **Several versions per platform.** Services often have the original, a remaster, and reissues of the same release; the page should be able to show all of them. Example: Metallica's *Load* has two official YouTube Music album playlists (`OLAK5uy_nn9_UEJTyKvU65JlglPBRMp9eWyxOTFWc` and `OLAK5uy_nf8K8yHuLThX0qvPGCIM2eBKndPale4vU`); the first version of this work shows one of them. This changes the one-link-per-platform unique index from [decisions/0007](0007-catalog-data-model.md).
- **Catalog-first search.** Search shows matching catalog items first, so users pick an existing item instead of searching the providers again.
- **Official YouTube Music album playlists** (`OLAK5uy_…`), preferred over fan playlists. They are not found by `search.list`, see "YouTube" above.
- **A Discogs link on song pages**, pointing to the song's album master.
- **Splitting `ProviderTrustOrder`** into a catalog-authority order and a display order.
- **`StreamingResultType.Artist`**, still deferred as in [decisions/0012](0012-discogs-provider.md).

## Consequences

- A click is fast again, whatever the providers do, but a share page is no longer final when it opens: its rows fill in over a few seconds.
- A share page URL is stable per item. Repeat clicks are free, apart from re-checking failed platforms.
- The share page response gains a per-row state and a page-level "checking / complete" status, and a new anonymous event-stream endpoint is added. The web share page changes from a list of found links to a fixed list of platform rows.
- `IStreamingProvider` gains a lookup method. Every future provider needs one (or states that it has none, like YouTube); the `add-streaming-provider` skill gains that step, so Deezer is built with it from the start.
- One new table (check results) and a migration. `ProviderLink.Confidence` and `LastVerifiedUtc`, unused since [decisions/0007](0007-catalog-data-model.md), are now filled.
- A merged search row (and so the cache) can carry an ISRC/UPC from a lower-ranked provider when the first result has none.
- The queue and the live-update hub are in memory: they serve one server instance, like the search candidate cache. A second instance needs a shared queue and event channel.
- A provider that has no lookup (YouTube) can only ever give a name match, so its row shows "(other version)" even when the link is the very item the user picked.
- The concurrency test for two simultaneous resolves (`EfCatalogRepositoryConcurrencyTests`) could already fail on `develop` with a Postgres deadlock, about once in eight runs. Resolve now retries a deadlock victim like the other conflicts.
- [roadmap.md](../roadmap.md)'s enrichment item points to this ADR, and the follow-ups above are added as future items. [architecture.md](../architecture.md)'s catalog and data-model sections are updated when the implementation lands.
