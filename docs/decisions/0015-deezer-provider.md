# 0015. Deezer provider

Status: accepted

## Context

[roadmap.md](../roadmap.md) lists Deezer as the next first-release provider. Most of what a provider needs is already settled: the `IStreamingProvider` shape and the SDK-vs-`HttpClient` rule ([decisions/0008](0008-youtube-search-sharepage-skeleton.md)), provider identity, trust order, merging, and failure isolation ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)), the required Song/Album search type ([decisions/0012](0012-discogs-provider.md)), and the exact-key lookup every provider needs ([decisions/0014](0014-resolve-time-catalog-enrichment.md)).

Deezer still raises questions those ADRs do not answer:

1. Deezer stopped accepting new developer apps in 2026, with no date for reopening ([Deezer Community](https://en.deezercommunity.com/features-feedback-44/app-registration-82666), [Macamp issue #18](https://github.com/holgerkrupp/Macamp/issues/18)). Its catalog endpoints still answer with no app id and no token. Is that a legitimate access path, and how is the provider registered when it has no credentials at all?
2. Deezer reports errors, including "not found", inside an HTTP `200` response. No provider so far does that.
3. Deezer's barcode lookup matches only the exact string Deezer stored.
4. Where Deezer sits in `ProviderTrustOrder`, and how complete and accurate its catalog is.
5. Deezer's guidelines require a visible Deezer logo.

## Decision

### Access: the public catalog endpoints, behind the ToS-review gate

hodnota calls only Deezer's public catalog endpoints on `https://api.deezer.com/`. They need no app id, no token, and no user login, and hodnota never plays audio. This is the documented "simple API", not an extracted or reverse-engineered credential.

The access is still not clean:

- Deezer's [Terms of Use](https://developers.deezer.com/termsofuse) say access is given "following the process indicated" on the developer site, and the site says "you have to login to accept the terms and conditions of the simple API". New apps cannot be created today.
- The project owner registered a Deezer account on 2026-10-02 and can read the developer documentation with it. No app exists.
- The terms allow non-commercial use only.

So Deezer follows Qobuz's rule: it may be built and used in development, but it must not reach real users until [roadmap.md](../roadmap.md)'s third-party ToS-review item confirms that this use is allowed, or Deezer reopens app registration. Unlike Qobuz, it is not dormant: there is nothing to configure, so it runs everywhere.

### Raw `HttpClient`, no token provider

Deezer publishes no .NET SDK. Following [decisions/0008](0008-youtube-search-sharepage-skeleton.md)'s rule, `Hodnota.Infrastructure.Providers.Deezer` uses a named `HttpClient`. With no credentials there is no credentials record and no token provider; the shape is Qobuz's (configuration, DTOs, API client, provider).

### Registration: always on

[decisions/0013](0013-tidal-provider.md)'s two-question table decides between fail-fast and config-optional registration. Neither question applies to a provider with no credentials:

| Provider | Can any contributor get credentials self-serve? | Do correct credentials work immediately? | Registration |
|---|---|---|---|
| Spotify | yes | no — Premium subscription required | optional |
| Qobuz | no — no developer portal | — | optional |
| YouTube, Discogs, Tidal | yes | yes | fail-fast |
| Deezer | no credentials needed | — | always on |

Deezer is registered unconditionally. There is no `Deezer` section in `appsettings.json` and no startup check in `Program.cs`. An on/off flag is not added; nothing needs it today.

### Search: one request per search

Confirmed against the live API on 2026-10-02.

| Search type | Request |
|---|---|
| Song | `GET search/track?q={query}&limit=5` |
| Album | `GET search/album?q={query}&limit=5` |

`limit` is honored, so the result cap is sent to Deezer instead of applied afterwards.

Result mapping:

- Track: `title` → name. Deezer's `title` already carries the version (`"Enter Sandman (Remastered 2021)"`; `title_short` and `title_version` hold the two parts), so the provider uses it as it is. This gives the same result as Tidal's `Title (Version)` rule and follows [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s bias: prefer a missed merge over a false merge. `isrc` → `Isrc` (track search results carry it).
- Album: `title` → name. `record_type` → `ReleaseType` (`album`→`Album`, `single`→`Single`, `ep`→`EP`, `compile`→`Compilation`, anything else → `Album`). Album search results carry **no UPC**; only the lookup below returns `upc`.
- Artist: `artist.name`, `"Unknown"` when missing — the same fallback as the other providers. Search results name only the main artist.
- Image: the album's `cover_big` (500 px), else `cover_xl` (1000 px), else `cover_medium` (250 px). This is the "smallest at least 300 px" rule the other providers use, applied to Deezer's fixed sizes.
- Link: `link` (for example `https://www.deezer.com/track/1483825212`) when it is an absolute http(s) URL, otherwise `https://www.deezer.com/{track|album}/{id}`.
- `ExternalId` is the plain Deezer id, as for Tidal.
- A result with no id or a blank title is skipped.

### Lookup: one call per code form

| Type | Request | Returns |
|---|---|---|
| Track (ISRC) | `GET track/isrc:{isrc}` | one track |
| Release (UPC/EAN) | `GET album/upc:{code}` | one album, with `upc` |

Each lookup returns one item, not a list, even when several Deezer items share the code. As [decisions/0014](0014-resolve-time-catalog-enrichment.md) requires, the provider keeps the item only when its own ISRC or barcode equals one of the key's codes.

The barcode lookup matches the stored string exactly. *Load (Remastered)* is stored as `602475158875`: that form finds it, while `0602475158875` and `00602475158875` answer "not found". So the provider tries the forms from `CatalogKeys.BarcodeVariants` one after the other (at most three calls) and stops at the first match. Tidal and Discogs, which accept every form, are asked once; Deezer is the first provider that needs all of them.

### Errors arrive inside HTTP 200

Deezer answers almost every error with status `200` and a body like `{"error":{"type":"DataException","message":"no data","code":800}}`. The API client therefore reads the `error` object after `StreamingSearchResponseReader` has handled the transport and status checks:

| `error.code` | Meaning | Handling |
|---|---|---|
| none | success | map the result |
| `800` | no data | not found: an empty result |
| `4` | quota exceeded | log a warning, like a `429`, and throw `StreamingProviderException` |
| any other (`600` invalid query, `700` service busy, …) | failure | throw `StreamingProviderException` |

This keeps [decisions/0014](0014-resolve-time-catalog-enrichment.md)'s "not found" and "failed" states apart: a quota hit is saved as "failed" and checked again later, not as a permanent "not found". As for every provider, there is no retry ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)).

### Rate limit: per server IP

Deezer's limit is 50 requests per 5 seconds **per IP address**. All hodnota users share the server's IP, so they share this limit. One search costs one call; one enrichment costs one call for a track and up to three for an album. A hit is logged and drops Deezer for that one search or check.

### Trust order: after Spotify, before YouTube

Deezer's search metadata is at Spotify's level: clean structured names, ISRC on tracks, no UPC on albums. Tidal and Qobuz return both keys from search. `ProviderTrustOrder.Order` becomes `discogs, qobuz, tidal, spotify, deezer, youtube`. Placing it below Spotify keeps today's link order unchanged for every share page, and Deezer owns a merged row only when none of the providers above it found the item.

### Catalog size and accuracy

Deezer says it has more than 120 million tracks. That is the vendor's own number, and it includes a large share of filler and AI-generated uploads.

A first live sample (2026-10-02, Deezer only):

| Query | Type | Found the real item? |
|---|---|---|
| nostromo ecce lex | album | yes — *Ecce Lex*, Nostromo |
| perfect cycle to whom it may concern | album | yes — A Perfect Circle, as a single |
| jinjer wallflowers | album | yes |
| boards of canada music has the right to children | album | yes |
| metallica load | album | yes — *Load (Remastered)*, plus the deluxe box set |
| go_a | album | yes — Go_A singles and the album |
| dakhabrakha baby | song | yes |
| okean elzy model | album | **no** — the album title on Deezer is Cyrillic ("Модель"), so the Latin spelling does not match |

Known weaknesses:

- **Transliteration.** A Latin query does not find a title stored in Cyrillic. Other providers have the same problem; it is not Deezer-specific.
- **Filler uploads.** A misspelled query ("metalica enter sandman") ranked a cover track by a filler account first. It does not merge with the real Metallica row ([decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md)'s normalized key), so it shows as a separate row; it does not replace a correct link.
- **Enrichment is protected.** The exact ISRC/UPC lookup is what puts a "found" link on a share page, and it accepts only an item with the same code.

A side-by-side comparison through the running app (2026-10-02, real providers, `hodnota_agent`), searching and resolving "metallica enter sandman" (song) and "metallica load" (album):

| Item | Qobuz | Tidal | Deezer |
|---|---|---|---|
| Song (Enter Sandman) | Found (exact ISRC) | Found (exact ISRC) | Found (exact ISRC) |
| Album (Load) | Found (exact UPC) | Found (exact UPC) | OtherVersion (name match) |

The album case is the interesting one: Qobuz and Tidal agreed on one UPC (`602478324055`) for the entity, but Deezer's own "Load (Remastered)" is stored under a different barcode (`602475158875`) — a different pressing of the same release. The lookup tried both the 12- and 13-digit forms of the canonical UPC and correctly got "not found" from Deezer both times, so the row fell back to the search-time name match rather than claiming a false exact match. This is the same "coverage depends on the pressing" behavior [decisions/0014](0014-resolve-time-catalog-enrichment.md) already documented for Discogs, not a bug in this provider.

### Attribution: no credit line now; the logo goes to the design work

Deezer's [logo guidelines](https://developers.deezer.com/guidelines/logo) say: "Each application using Deezer API/SDKs must include a clearly visible Deezer Logo." This asks for the logo, not for a credit linked back to Deezer. Tidal's plain-text credit ([decisions/0013](0013-tidal-provider.md)) met part of Tidal's written link-back condition; a Deezer text line would meet no written Deezer rule. So no credit line is added. Each share page row already names the platform.

The logo requirement joins Tidal's in [roadmap.md](../roadmap.md)'s ToS-review item. Brand logos are a UI-design question, as [decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md) already decided: these provider presentation rules are an input to the visual-design work, not a per-provider patch.

## Consequences

- A sixth `IStreamingProvider`, and the first one with no credentials and no configuration. It runs in every environment, including CI test hosts, though the tests never call it.
- A new error-handling shape: errors inside HTTP `200` bodies, read after the shared response reader.
- `ProviderTrustOrder` gains `deezer` between `spotify` and `youtube`. Existing share pages keep their link order; they gain a Deezer row on the next check of a never-checked platform ([decisions/0014](0014-resolve-time-catalog-enrichment.md)).
- The Deezer rate limit is shared by every user of one server IP. A busy server will see Deezer drop out of searches more often than the per-token providers.
- Deezer's terms allow non-commercial use only. Like Tidal's, this limits the open LICENSE question in [roadmap.md](../roadmap.md).
- Deezer must not reach real users until the ToS-review item resolves its access (registration closed, terms not accepted through an app) and its logo requirement.
- [roadmap.md](../roadmap.md)'s Deezer sub-item is checked off, and its ToS-review item gains a Deezer clause. [architecture.md](../architecture.md)'s streaming-provider integration section gains a Deezer sentence.
