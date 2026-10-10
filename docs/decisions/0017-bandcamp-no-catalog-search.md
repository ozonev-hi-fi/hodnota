# 0017. Bandcamp: no catalog-search provider

Status: accepted

## Context

[roadmap.md](../roadmap.md)'s last unchecked sub-item under "Implement the remaining first-release providers" is Bandcamp, listed from the start with an open question:

> Bandcamp — selling platform with limited preview playback (1-2 plays/track); not a full `IStreamingProvider`, but still produces a shareable link. Interface shape decided when this item is picked up.

Every previous provider pickup (Discogs, Tidal, Deezer, Apple Music) started from some real, usable API surface, even when access was awkward — a self-serve token, an undocumented-but-working endpoint, or (Qobuz) credentials obtained by a method that conflicts with the vendor's terms but that at least calls a real catalog-search API once obtained (see `CLAUDE.local.md`'s Qobuz entry). Bandcamp is different: there is no catalog-search API to call at all, by any method.

**Official API scope.** `bandcamp.com/developer` describes a gated, request-only API (no self-serve signup; access is requested by email), authenticated with OAuth 2.0, and lists exactly three APIs: an Account API, a Sales Report API, and a Merch Orders API — all for labels and merchandise fulfillment partners managing their own accounts. There is no catalog, search, or fan-facing API of any kind. Even an official, hand-granted access request would not produce the capability this provider needs — unlike Qobuz, where the gate is only on obtaining credentials for an API that otherwise does what's needed.

**Acceptable Use Policy.** Bandcamp's Terms of Use (`bandcamp.com/terms_of_use`) restrict Content use to "personal, non-commercial use" and bar exploiting Content commercially; its incorporated Acceptable Use and Content Moderation Policy goes further and explicitly prohibits scraping site content. Every community tool that searches Bandcamp's catalog today (`bandcamp-scraper`, `bandcamp-fetch`, yt-dlp's Bandcamp extractor) works by scraping search-result or embed-page HTML — there is no technique here comparable to Qobuz's `app_id` extraction, which at least calls a real, intended API once the value is obtained. Scraping Bandcamp's search or catalog pages is a direct violation of a stated policy, not a gray area.

**Conclusion of research:** no automated catalog search, and no ISRC/UPC lookup, can be built for Bandcamp today without violating its terms. This project does not build it.

[decisions/0011](0011-spotify-provider-and-cross-provider-result-merging.md) already anticipated a provider like this: it keyed `ProviderTrustOrder` on provider-code strings specifically so it "already accommodates the roadmap's odd future entries (Bandcamp, Discogs) that won't be full `IStreamingProvider`s." This ADR confirms that anticipation was correct for Bandcamp, for a terms-of-service reason rather than an interface-shape reason — Discogs turned out to fit `IStreamingProvider` as-is ([decisions/0012](0012-discogs-provider.md)); Bandcamp doesn't get that far, because there's nothing to search in the first place.

Separately, [ideas.md](../ideas.md#paste-a-link-instead-of-searching) ("Paste a link instead of searching," on the roadmap but not yet implemented) already covers a different access pattern: a user supplies a link they already have, and the matching provider is asked to resolve only that one id — not to search. That idea's own "Open questions" section already flags that `IStreamingProvider` has no such per-id resolve member today, and that adding one is a decision for whenever that feature is picked up, since it affects every provider's interface, not just Bandcamp's.

## Decision

No `IStreamingProvider` implementation, and no scraping-based mechanism of any kind, is built for Bandcamp. Bandcamp gets a link on a share page only when a user supplies one directly, and only once "Paste a link instead of searching" is implemented.

That feature's own ADR — written when it is picked up, not here — owns the URL-resolve interface shape (a decision for every provider that idea covers, not Bandcamp-specific) and must separately decide how reading a *single, user-identified* Bandcamp page would work. Fetching one specific page a user has already found and pasted is a narrower, plausibly different question from automated search or listing scraping, but it is not decided here — it stays an open question for that future ADR.

Until then, Bandcamp remains exactly as it is today: a seeded `Platform` row (`PlatformCodes.Bandcamp`, `PlatformType.DigitalStore`, from the original `InitialCreate` migration) with a web UI label but no provider, which already renders as no row on a share page per [decisions/0014](0014-resolve-time-catalog-enrichment.md)'s existing rule for a seeded-but-provider-less platform.

**Rejected alternatives:**
- *Scrape anyway, like Qobuz.* Rejected: Qobuz's ToS conflict is about how credentials for a real API are obtained; Bandcamp's conflict would be scraping itself, which its policy names and bans outright. The two aren't comparable risk levels.
- *Register a no-op `IStreamingProvider`* (`Supports` returns `false` for everything, `SupportsLookup` returns `false`). Rejected: there would be nothing for it to do — no search, no lookup, no name-lookup — so it would add a class and a DI registration that exist purely to be inert. `ProviderTrustOrder`'s string-keyed design (see Context) means no such placeholder is needed to "reserve a spot."
- *Request official API access by email anyway.* Rejected: the developer page already states the scope of what's grantable (Account/Sales/Merch), and none of it is a catalog-search capability — asking would not change the outcome, unlike Qobuz where the gate was genuinely just credentials for an existing search endpoint.

## Consequences

- [roadmap.md](../roadmap.md)'s Bandcamp line is reworded to state this decision and link here; it stays unchecked, now explicitly blocked on "Paste a link instead of searching" rather than open-ended.
- [ideas.md](../ideas.md#paste-a-link-instead-of-searching) gets a short note that Bandcamp is a future URL form not yet specified there, pointing here.
- [architecture.md](../architecture.md)'s Purchase Platforms section gets a one-line note on Bandcamp, mirroring the Metadata Platforms section's existing Discogs line, pointing here.
- No code, test, or migration changes — nothing is implementable yet.
- The still-open, UAT-gate-deferred "third-party streaming-API terms-of-service review" item in [decisions/0010](0010-uat-production-readiness-gate.md) no longer needs to cover Bandcamp as a shipped provider's ToS — there is no provider to review. If "Paste a link instead of searching" later adds a single-page Bandcamp fetch, that review item should be revisited for it specifically.
