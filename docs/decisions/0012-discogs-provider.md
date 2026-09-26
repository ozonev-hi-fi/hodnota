# 0012. Discogs provider

Status: accepted

## Context

[roadmap.md](../roadmap.md) names Discogs as the next provider to implement, prioritized ahead of Tidal/Deezer/Apple Music/Bandcamp: [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s YouTube addendum documents an observed case where YouTube's free-text video search simply never surfaced a matching item for a real query, and names Discogs's structured metadata — not further YouTube tuning — as the intended fix.

Discogs is unlike every provider implemented so far. YouTube, Spotify, and Qobuz are all streaming services: a match is something a listener can play. Discogs is a community-maintained metadata database (with a separate marketplace half this project does not use) — a match is a page describing a release, with no playback at all beyond an occasional embedded YouTube sample on some pages. The roadmap deliberately left Discogs's interface shape as "decided when this item is picked up," rather than assuming it would fit `IStreamingProvider` unchanged.

Four questions needed real answers before writing any code, none of them mechanical:

1. Does Discogs's result shape fit the existing `IStreamingProvider`/`StreamingSearchResult` contract, or does it need a new interface?
2. Which Discogs entity should a share page link to — Discogs models one album as a master release grouping many specific pressings?
3. What does Discogs's own access model require (self-serve token vs. something closer to Qobuz's no-portal situation)?
4. Discogs's API Terms of Use impose a real obligation — a "Data provided by Discogs" credit linked back to the specific page — that no other provider's terms require in this form. Does this ship now or wait for the roadmap's general, UAT-gate-deferred provider ToS review?

A first implementation of this ADR mapped every Discogs `release` and `master` hit onto a search row and ranked Discogs first in `ProviderTrustOrder`. Review found that this pushed every song result below up to ten album rows (Discogs returns no tracks), let an arbitrary pressing — not the master — own a merged row's link, release type, and barcode, and stored Discogs's free-form `barcode` array entries (label codes, matrix strings) as a release's UPC natural key. The decisions below replace that first version.

## Decision

### `IStreamingProvider` is reused; no new interface

Nothing in `IStreamingProvider` or `StreamingSearchResult` (`Hodnota.Application.Catalog`) assumes playable content. Every existing provider already returns `ProviderLinkCandidate`s that are just external page URLs — YouTube's, Spotify's, and Qobuz's links carry no player, no embed, and no "is this playable" flag anywhere in the model. The playable/informational distinction lives entirely in `Platform.Type` (`Hodnota.Domain.Catalog.PlatformType`), consumed only at render time by the web `SharePage`'s `CATEGORY_LABELS` map, which already routes `PlatformType.Database` to a non-transactional "Discover" bucket alongside `Aggregator` — a bucket that existed before this feature but had no platform using it.

Discogs implements `IStreamingProvider` directly, following the raw-`HttpClient` pattern (`Hodnota.Infrastructure.Providers.Discogs`) — Discogs publishes no official first-party .NET SDK, so [decisions/0008](0008-youtube-search-sharepage-skeleton.md)'s "an official free SDK when the vendor provides one, a raw `HttpClient` otherwise" rule selects the same path Spotify and Qobuz already took.

### Search takes a required Song/Album type

The search request gains a required type — `SearchRequest(string Search, CandidateType Type)` — and `IStreamingProvider.SearchAsync` receives it as a `StreamingResultType`. Each provider searches only that type: Spotify sends `type=track` or `type=album`, YouTube `type=video` or `type=playlist`, Qobuz filters its single combined response, and Discogs searches only for albums, returning nothing (without an HTTP call) for a song search. The web search page offers the choice as two radio buttons, "Song" pre-selected.

This is what makes Discogs fit a mixed provider set at all: a Discogs-first trust order is only harmful when Discogs's album-only results compete with other providers' songs for the same capped result list. With the type filter, a song search never involves Discogs, and an album search is exactly where Discogs is the most trustworthy source. It also follows from a narrower view of what search is for: helping the user find the item they mean from a few remembered words, shown as a plain artist + title pair. Verifying that item across providers and building a correct catalog entry is a separate step, after the user picks a result — see the "Out of scope" section below.

A consequence: a track share page never carries a Discogs link. Discogs has no track-level pages to link to, and linking a track to its album's page is left to the enrichment work named below.

### Link to the highest Discogs entity that exists: the master, else the release

Discogs distinguishes a **master release** (the grouping of all pressings of what a listener would call "the same album") from a **release** (one specific pressing/edition). A share page links to the master whenever one exists, so the reader lands on the page that leads to every edition. Many releases — often ones pressed only once — have no master; for those, the release page itself is the highest entity and is linked instead.

An album search sends two Discogs requests in parallel: `type=master` (5 results) and `type=release` (25 results). Masters come first; releases are kept only when their `master_id` is empty or zero (a release with a master is one of that master's pressings, already represented at the master level). The combined list is capped at 5 Discogs rows. The larger release batch exists because popular queries return mostly pressings of masters, leaving few master-less releases in a small batch. If either request fails, the whole Discogs search fails and `CatalogSearchService` skips Discogs for that query, the same as any single-request provider failure — a partial Discogs result (masters only, or master-less releases only) would silently hide the other kind.

A Discogs master id and a Discogs release id are separate number spaces — master `13814` and release `13814` are unrelated items. `ExternalId` is therefore stored with a type prefix, `master:13814` / `release:13814`, so the `(PlatformId, ExternalId)` unique index never treats them as one.

### Result mapping

Discogs's `/database/search` has no separate artist field on a search result — a result's `title` comes back as one combined string, conventionally `"Artist - Release Title"`. `DiscogsStreamingProvider` splits on the first `" - "` to recover `ArtistName`/`Name`; a title with no separator becomes the release name with artist `"Unknown"` (the same fallback the other providers use), not an error — malformed input from a community-edited database is expected, not exceptional. The artist part then loses two Discogs-only markers that no other provider uses and that would otherwise break cross-provider matching and display: the numeric disambiguation suffix Discogs adds to tell same-named artists apart (`"Placebo (3)"` → `"Placebo"`) and the trailing `*` marking an artist name variation (`"Beyoncé*"` → `"Beyoncé"`).

A master-less release's `format` array (e.g. `["Vinyl", "LP", "Album"]`, `["7\"", "Single"]`) describes one pressing, and is searched for the keywords `Compilation`, `Single`, `EP`, `Live`, defaulting to `Album` otherwise — the same best-effort mapping style Spotify's `album_type` switch uses for `ReleaseType`, extended to reach `EP`/`Live`. A master's `format` describes its pressings, not the master, so a master uses the same keyword mapping only when verified against a live response to be master-level data; otherwise it defaults to `Album`.

Discogs exposes no ISRC anywhere in its API; `Isrc` stays null. `barcode` is not mapped to `Upc`: a master has no single barcode, and a release's `barcode` array mixes real UPC/EAN barcodes with label codes, matrix/runout strings, and rights-society codes in no fixed order or format. Using one as a unique natural key would either reject a later, unrelated release sharing a label code or keep an unnormalized value that never matches another provider's UPC. Barcode-based matching belongs to the enrichment step named below, where it can be normalized and validated.

When a Discogs item has no uploaded image, Discogs returns a placeholder (`spacer.gif`) as `cover_image`, not an empty value. `PickImage` treats that placeholder as "no image", so a placeholder can never replace another provider's real artwork on a merged row.

### Authentication: Personal Access Token, not Consumer Key/Secret, not OAuth

Discogs offers three access tiers, all self-serve from the account's own Developer Settings page with no manual approval — a materially different situation from Qobuz's no-self-serve-portal problem ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s Qobuz addendum): a Personal Access Token generated against one Discogs account; a Consumer Key/Secret pair issued to a registered "application" without tying it to a specific user; or full OAuth 1.0a, needed only to act on behalf of *other* Discogs users (placing marketplace orders, reading another user's collection), which hodnota's read-only database search never does.

A Personal Access Token is used: one config value (`Discogs:Token`), sent as `Authorization: Discogs token={token}`, no token exchange or refresh (unlike Spotify's Client Credentials flow), no `DiscogsAccessTokenProvider` class. Discogs requires every request to carry a unique `User-Agent` identifying the calling application (part of qualifying for the higher, 60-requests/minute authenticated rate limit, versus 25/minute unauthenticated) — this is a fixed string in `DiscogsConfiguration`, not a config value, since it doesn't vary by environment. Both headers are set once on the named `HttpClient`, the same way Qobuz sets its static credentials. An album search costs two requests, so the authenticated limit covers 30 album searches a minute.

**Registration is fail-fast**, following YouTube's pattern, not Spotify's/Qobuz's config-optional guard. [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s Spotify addendum established config-optional as the exception, justified only when "valid credentials don't guarantee working access" (Spotify's Premium-subscription gate; Qobuz's lack of any confirmed self-serve path at all). Nothing in Discogs's documented access model resembles either gotcha — a correctly generated Personal Access Token works immediately. The app fails at startup if `Discogs:Token` is missing, the same way it already does for `YouTube:ApiKey`.

### Trust order: ranked first, above Qobuz

[roadmap.md](../roadmap.md)'s own "source-of-truth authority order" item already names a working hypothesis: "Discogs as the top authority for release/artist metadata correctness." `ProviderTrustOrder` reflects the same judgment: Discogs is ranked first, above Qobuz. With the Song/Album type filter, this ranking only ever applies to album searches — the case it is meant for — so a Discogs master or master-less release owns a merged album row, and its Discogs link is the first link on that album's share page.

### Attribution ships now, not deferred to the general ToS review

[decisions/0010](0010-uat-production-readiness-gate.md) defers a full "third-party streaming-API terms-of-service review" (logo usage, presentation rules) to after the UAT gate, and [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md) kept the pre-UAT UI deliberately neutral (no logos, no brand colors, no platform-styled buttons) precisely so that future review cannot force a redesign. That deferral was always about presentation *style* choices where no obligation exists yet.

Discogs's API Terms of Use are different in kind, not degree: displaying Discogs-sourced data without a "Data provided by Discogs" credit, hyperlinked back to the specific Discogs page, is a stated condition of using the API at all — not a style guideline that can safely wait. The credit is added now, in `web/src/pages/SharePage.tsx`, as one plain-text line placed after the link groups when the page contains a Discogs link, pointing at that specific master/release page — text-only, matching ADR 0011's existing neutral-UI style, so it does not reopen that deferred review either.

### Out of scope: resolve-time enrichment

Today a share page is built from the links collected during search. The intended direction is different: search only helps the user find the item, and after the user picks a result, every provider is queried again for that exact artist + title (and, for Discogs, by the other providers' UPC — `barcode` search, then the release's master) to build a verified catalog entry once, which later visits reuse instead of searching again. That changes how catalog entities are created and is its own decision (a future ADR 0013), not part of adding Discogs.

Discogs artist-only and label-only search matches are also dropped, not approximated. `StreamingResultType` has only `Track`/`Release`; adding `Artist` (a `StreamingResultType.Artist` case, an `EfCatalogRepository.ResolveArtistAsync` branch, a `CandidateType.Artist` contract case) is deliberately deferred — the domain schema (`SharePage.ArtistId`, `ProviderLink.ArtistId`) is already waiting for it.

## Consequences

- Establishes the first `IStreamingProvider` implementation that is not a streaming/playback service, exercising `Platform.Type = Database` and the web UI's existing "Discover" bucket for the first time.
- The search API contract changes: `SearchRequest.Type` is required, and every `IStreamingProvider` implementation takes the requested type. Future providers must honor it.
- Discogs only takes part in album searches; track share pages never carry a Discogs link.
- An album share page links to a Discogs master when one exists, otherwise to the specific release. Releases on Discogs's first 25 release hits that belong to a master are dropped from results rather than shown as a second row for the same album.
- An album search costs two Discogs requests.
- Discogs never sets `Upc`; releases whose only barcode source is Discogs have none until the enrichment step exists.
- `ProviderTrustOrder.Order` gains `discogs` at index 0, ranked above Qobuz; this changes album-search merge priority and every merged album share page's visible link order.
- The web `SharePage` gains its first provider-specific UI behavior (a Discogs attribution credit) — a deliberate, narrow exception justified by an actual contractual requirement, not a precedent for adding more per-provider UI without similarly concrete justification.
- [roadmap.md](../roadmap.md)'s Discogs sub-item is checked off, and a new item names resolve-time enrichment (future ADR 0013).
- [architecture.md](../architecture.md)'s streaming-provider integration section gains a Discogs sentence alongside YouTube/Spotify/Qobuz.
