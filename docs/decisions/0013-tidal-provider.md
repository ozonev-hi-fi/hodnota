# 0013. Tidal provider

Status: accepted

## Context

[roadmap.md](../roadmap.md) lists Tidal as the next first-release provider, after Spotify, Qobuz, and Discogs. Most of what a provider needs is already settled: the `IStreamingProvider` shape and the SDK-vs-`HttpClient` rule ([decisions/0008](0008-youtube-search-sharepage-skeleton.md)), provider identity, trust order, merging, and failure isolation ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)), and the required Song/Album search type ([decisions/0012](0012-discogs-provider.md)).

Tidal still raises questions those ADRs do not answer:

1. Tidal's catalog API (`openapi.tidal.com/v2`) uses the JSON:API format: a search response holds only `{type, id}` references, and the full track/album/artist/artwork objects arrive in a separate `included` list. No provider so far needed that kind of mapping.
2. Tidal's Developer Guidelines require "TIDAL branding together with a link back to the TIDAL Service" and say content "must be attributed to TIDAL with our logo". [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md) keeps the UI text-only until the UAT-gated ToS review; [decisions/0012](0012-discogs-provider.md) allowed one narrow exception for a contractual attribution requirement.
3. Tidal's access model — self-serve, but the platform still calls itself "Beta" and has changed its search contract without notice (August 2026) — needs a fail-fast vs config-optional call.
4. Where Tidal sits in `ProviderTrustOrder`.

## Decision

### Raw `HttpClient`, OAuth2 Client Credentials

Tidal publishes official SDKs for Web (TypeScript), Android, and iOS, but none for .NET. Following [decisions/0008](0008-youtube-search-sharepage-skeleton.md)'s rule, `Hodnota.Infrastructure.Providers.Tidal` uses a named `HttpClient`, the same path as Spotify, Qobuz, and Discogs.

Authentication is the Client Credentials grant (`POST https://auth.tidal.com/v1/oauth2/token`, HTTP Basic with client id/secret, `grant_type=client_credentials`) — hodnota only searches Tidal's public catalog and never acts for a Tidal user. The token is cached and refreshed shortly before expiry, and a `401` from search drops the cached token and retries once, the same way `SpotifyAccessTokenProvider`/`SpotifyApiClient` already work.

### Search: one request per search, JSON:API mapping

Confirmed against the live API on 2026-09-30 (a probe with a real app's credentials), not taken from documentation, because Tidal changed this contract without notice in August 2026.

A search sends `GET https://openapi.tidal.com/v2/searchResults` with `Accept: application/vnd.api+json` and these query parameters:

| Search type | `filter[query]` | `include` |
|---|---|---|
| Song | the query | `tracks,tracks.artists,tracks.albums.coverArt` |
| Album | the query | `albums,albums.artists,albums.coverArt` |

- **One request is enough.** Nested includes return the tracks, their artists, their albums, and the album artwork together. No second request is needed.
- **The `include` commas must stay literal.** A URL-encoded comma (`%2c`) is read as part of one path name and answered with `400`.
- **`countryCode` is optional.** A search without it succeeds, so it is sent only when the optional `Tidal:CountryCode` key is set — the same approach as Spotify's `market`. hodnota links to a page, not to playback, so no country bias is needed by default.
- **`data` is an array with one `searchResults` item.** Its `relationships.tracks` (or `.albums`) lists `{id, type}` references in relevance order. The full objects are in `included`, in a different order. The mapper therefore walks the reference list and looks each item up in `included` by `(type, id)`; a reference with no matching `included` object is skipped, not an error.
- **Page size is fixed at 20 per type.** `page[limit]` is accepted and ignored, so the provider takes the first 5 results, matching the other providers.
- **No rate-limit headers are returned.** A `429` is the only signal (see below).

Result mapping:

- Track: `title` (plus `version`, see below); `isrc` → `Isrc`.
- Album: `title`; `barcodeId` → `Upc`; `albumType` → `ReleaseType` (`ALBUM`→`Album`, `SINGLE`→`Single`, `EP`→`EP`, anything else → `Album`). Only `ALBUM` and `SINGLE` were seen in the probe; `EP` is assumed, and is harmless if Tidal names it differently. An album also has a second `type` attribute with the same value, which is ignored.
- Artist names come from the `artists` relationship (`attributes.name`), joined with `", "`, and `"Unknown"` when there are none — the same fallback as the other providers.
- Image: a track has no artwork of its own, so it uses its first album's `coverArt` (`tracks.albums.coverArt`). The artwork's `attributes.files` list sizes 1280, 1080, 750, 640, 320, 160, and 80 px. The provider uses Spotify's rule: the smallest file at least 300 px wide (320 px), otherwise the largest.
- Link: the `externalLinks` entry whose `meta.type` is `TIDAL_SHARING` (for example `https://tidal.com/browse/track/109813974`), otherwise `https://tidal.com/browse/{track|album}/{id}`.
- `ExternalId` is the plain Tidal id; track and album ids are not prefixed, because Tidal track, album, and artist ids are already separated by the `(PlatformId, ExternalId)` index together with the result type.

`version` is `null`, `""`, or free text, and is not limited to recording types: the probe returned `"Bonus Track"` and `"Jungle Cruise Version Part 1"` besides `null`. A non-blank version is appended to the name as `Title (Version)`. Otherwise a live or alternate recording could get the same name as the studio track and merge into one row; [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s rule is to prefer a missed merge over a false merge. The cost is accepted: a harmless version like `Bonus Track` may prevent one merge with another provider's plain title.

### Token lifetime

The token endpoint returned `expires_in: 14400` (4 hours), `token_type: Bearer`, and an empty `scope`. The token provider reads `expires_in` from the response instead of hard-coding it, and refreshes 60 seconds before expiry, like Spotify's.

### Rate limits: no retry

Tidal publishes no limit numbers; limits are per client id, and a `429` carries `Retry-After`. As for every other provider, the client does not wait and retry: `StreamingSearchResponseReader` logs the `429` and throws, and [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s failure isolation drops Tidal for that one search.

### Registration is fail-fast

[decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s Spotify addendum made config-optional the exception, used only when valid credentials do not guarantee working access. That is a two-part test:

| Provider | Can any contributor get credentials self-serve? | Do correct credentials work immediately? | Registration |
|---|---|---|---|
| Spotify | yes | no — Premium subscription required | optional |
| Qobuz | no — no developer portal | — | optional |
| YouTube, Discogs, Tidal | yes | yes | fail-fast |

Tidal's Developer Dashboard is self-serve with a free Tidal account, and search needs no extra access approval. The app fails at startup when `Tidal:ClientId` or `Tidal:ClientSecret` is missing, the same way it does for `YouTube:ApiKey` and `Discogs:Token`. The "Beta" label alone is not a reason for config-optional; if Tidal later adds a Spotify-style access gate, that is the trigger to revisit this.

### Trust order: after Qobuz, before Spotify

Tidal's search results carry clean structured metadata with ISRC on tracks and UPC on albums, the same level as Qobuz and above Spotify (whose search response has no UPC). `ProviderTrustOrder.Order` becomes `discogs, qobuz, tidal, spotify, youtube`. Placing it below Qobuz keeps today's merged rows and share-page link order unchanged whenever Qobuz answers; Tidal owns a row only when Qobuz has no match.

### Attribution: a text credit now, the logo waits for the ToS review

Like Discogs's credit ([decisions/0012](0012-discogs-provider.md)), Tidal's branding-and-link-back requirement is a condition of API use, not a style choice. It ships now as one plain-text line: `web/src/pages/SharePage.tsx` shows "Content provided by TIDAL", linked to that share page's Tidal page, when the page has a Tidal link; `web/src/pages/SearchPage.tsx` shows the same line, linked to `https://tidal.com`, when any result row was found on Tidal.

The logo requirement is not met by this. Adding brand logos is exactly the UI-presentation question [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md) deferred to the UAT-gated ToS review, and that review now has a concrete Tidal item to resolve. This is the second provider-specific UI exception, justified the same way as the first — a written condition of API access — and still not a precedent for per-provider UI without one.

### Out of scope

ISRC/UPC-based matching stays deferred. Tidal is the third provider that fills `Isrc`/`Upc`, but `SearchResultKey`/`SearchResultMerger` stay normalized-key-only, as in [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s Qobuz addendum; exact-match lookup belongs to the resolve-time enrichment work (future ADR 0014).

## Consequences

- A fifth `IStreamingProvider`, and the first one whose response format (JSON:API with `included`) needs reference resolution during mapping.
- Every developer running hodnota locally now needs a Tidal app's client id and secret (free, self-serve) in `.env.local`, or the API does not start.
- `ProviderTrustOrder` gains `tidal` between `qobuz` and `spotify`. Share pages without a Qobuz link now list Tidal before Spotify and YouTube.
- The web `SharePage` and `SearchPage` gain a second provider credit line.
- Tidal's Developer Terms allow only non-commercial applications. That fits the project today, but it limits the open [roadmap.md](../roadmap.md) LICENSE question: a later commercial path (such as the BUSL option) would have to drop Tidal or get separate permission. The ToS-review roadmap item now tracks this and the logo requirement.
- Tidal's developer platform is in Beta and has changed its search contract without notice before. A future silent change shows up as Tidal results quietly disappearing (failure isolation turns the error into a log warning), not as a failed search.
- [roadmap.md](../roadmap.md)'s Tidal sub-item is checked off, and the resolve-time enrichment item is renumbered to future ADR 0014.
- [architecture.md](../architecture.md)'s streaming-provider integration section gains a Tidal sentence.
