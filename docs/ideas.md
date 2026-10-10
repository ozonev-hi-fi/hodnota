# Ideas

Rough, unsequenced ideas — a place to write something down before it has a slot in [roadmap.md](roadmap.md), so it isn't lost and isn't forced into the roadmap's "epic-level plan" shape too early.

Each entry has:
- **Status** — `idea` (not yet prioritized), `on roadmap` (has a one-line roadmap item linking back here), or `decided → ADR NNNN` (an ADR exists; this entry is kept as a one-line pointer so there's never two sources of truth).
- **Problem** — what's missing or awkward today.
- **Example** — a concrete case.
- **Settled** — decisions already made about the idea, even though nothing is built yet.
- **Open questions** — left for the ADR (if the idea needs one), written when work on the idea starts (see [workflow.md](workflow.md)).

An idea moves to `roadmap.md` as one line once it's prioritized; an ADR entry shrinks this one to a pointer once it's accepted.

## Add proper logging and audit

**Status:** high priority idea.

**Problem.**
1. No logging at all, dev console only, not acceptable for UAT. Something happens - nobody knows what.
2. Once somebody (person or system actor) change something (especially in catalog) there is no history for that. Critical once system have editing functionality and, for example, the [User-suggested links](#user-suggested-links)

---

## Paste a link instead of searching

**Status:** on roadmap — see [roadmap.md](roadmap.md).

**Problem.** The search field only accepts free text today (`web/src/pages/SearchPage.tsx` → `POST /api/catalog/search`). Users often already have a link to the track or release from one service and have to retype the artist/title instead of just using it.

**Example.** Paste `https://youtu.be/1gvOSUZGXdo?si=EXAMPLE123` (a video that isn't the official audio) → the app reads the video, finds the official audio `sg8Y68jzDIY` by search, and opens a share page with YouTube, YouTube Music, Spotify, Qobuz, Tidal, etc. Paste `https://music.youtube.com/watch?v=sg8Y68jzDIY` (the official audio itself) → the same page, and the pasted link becomes the YouTube link with no search. Paste `https://open.qobuz.com/track/455140968` or `https://play.qobuz.com/album/plputwne9qt9g` (Perfect Cycle – *To Whom It May Concern*) → same flow, starting from Qobuz instead.

**Settled:**
1. A link is detected only when the whole trimmed input is one absolute `http(s)` URL. Free text mixed with a URL is treated as plain text search, not as a link.
2. Only track and release links are in scope — the catalog has no artist kind yet (see "Artist share pages" below). An artist or playlist link, or any link from a site we don't recognize, is reported as not supported (see the table below) rather than guessed at.
3. The pasted link is the *only* source of information: the matching provider is asked to resolve its own id, and whatever it returns (artist, title, ISRC/UPC if any) drives everything downstream. The Song/Album radio on the search form is ignored — the link's own type wins.
4. The catalog is checked first (`ProviderLink (PlatformId, ExternalId)` is already unique and already looked up by resolve). If the pasted id is already linked to a catalog entity, the user goes straight to its existing share page, with no call to read the pasted id. The rest is the same as a click on an existing item in [decisions/0014](decisions/0014-resolve-time-catalog-enrichment.md): platforms whose last check failed, or that were never checked, are checked again.
5. On a successful read, the user goes directly to the share page — found or created the same way a clicked search result already works (`SharePageService.ResolveAsync` and friends). There's no extra confirmation step in between.
6. On failure, the search dropdown shows one row that can't be clicked, with text that depends on the reason:

   | Case | Text |
   |---|---|
   | Unrecognized site | "Links from this site aren't supported" |
   | Recognized platform, no provider implemented yet (e.g. Apple Music) | "Apple Music links aren't supported yet" |
   | Provider not currently running (Spotify dormant, Qobuz without credentials) | "Can't read Spotify links right now" |
   | Artist link | "Artist links aren't supported yet" |
   | Playlist link | "Playlist links aren't supported" |
   | Short link that needs a redirect (point 11) | "This short link isn't supported, open it and copy the full link" |
   | Item not found, or the provider call failed | "Could not find this item, try another link" |

   A YouTube Music album playlist (`OLAK5uy_…`) is an album, not a playlist, and is read as a release.

   There is no text-search fallback and no fetching/scraping of the pasted page — only a provider's own API is ever called, and only for sites we already recognize.
7. The canonical link is always rebuilt from provider + id, the same way every other link on a share page already is (e.g. `YouTubeStreamingProvider`'s `BuildUrl`). The pasted URL itself — tracking parameters (`si=`), short-link form, locale prefix — is never stored or shown.
8. The page shows one link per platform, as it does today. A pasted link never adds a second link for a platform. It fills its platform's slot when the slot is empty, and it replaces an existing link only under [decisions/0014](decisions/0014-resolve-time-catalog-enrichment.md)'s rule (an exact-key match replaces a name match). There's deliberately no "other links" bucket for extra pasted items (e.g. a second video of the same song). The page is shared by everyone who opens it, and a bucket like that would grow without any moderation step. A pasted YouTube video is kept as the YouTube/YouTube Music link only when its uploader is the artist: the artist's own channel or its automatic "Artist - Topic" channel. That is the same uploader rule 0014's enrichment search uses (`SearchResultNameMatcher`). Any other pasted video (a music video, a fan upload) is only read to learn the artist and title, and isn't kept anywhere.
9. A page created from a non-YouTube paste still gets YouTube/YouTube Music links through the existing enrichment rule in [decisions/0014](decisions/0014-resolve-time-catalog-enrichment.md) (one by-name YouTube search per item that has no YouTube link yet) — nothing extra is needed for that. YouTube Data API cost of a paste (`search.list` is 100 calls/day for the whole project, see [youtube-music-search.md](youtube-music-search.md)):

   | Paste | YouTube cost |
   |---|---|
   | Already in the catalog (point 4) | 0, unless 0014 re-checks a failed or never-checked YouTube row |
   | YouTube video from the artist's own or "Topic" channel | 1 unit (`videos.list`, 10,000/day bucket). It becomes the link (point 8), so no search |
   | Any other YouTube video | 1 unit + 1 `search.list` call (0014's by-name search) |
   | Non-YouTube link, the item has no YouTube link yet | 1 `search.list` call (0014's by-name search) |
10. Matching a pasted YouTube link to a real artist/title is best-effort and can fail closed: Music category only, "Artist - Topic" channel preferred, and if the parse isn't clear, it's reported as not found (see the table in point 6) rather than guessed. The actual matching rules live in [youtube-music-search.md](youtube-music-search.md) and aren't restated here.
11. Short links that need an HTTP redirect to resolve (e.g. `spotify.link/AbC123`) are out of scope for v1 — following a user-supplied redirect is a server-side request forgery (SSRF) surface and deserves its own design. They get the short-link text from the table in point 6.
12. Logged in users only, same as search today (`[Authorize]` on `/api/catalog/search`). Future, not-yet-designed users of this same mechanism: a mobile share-sheet integration and a Telegram bot that watches for streaming links (both mentioned in [architecture.md](architecture.md)'s Components section) will need their own auth story.

**Known limitation carried over from [decisions/0014](decisions/0014-resolve-time-catalog-enrichment.md):** YouTube has no exact lookup, so even the exact item the user pasted shows on its own page as a name match ("(other version)"), not as a confirmed exact match.

**Example URL forms to recognize:**
- `youtu.be/ID`, `youtube.com/watch?v=ID`, `music.youtube.com/watch?v=ID`, `music.youtube.com/playlist?list=OLAK5uy_…`
- `open.qobuz.com/{track|album}/ID`, `play.qobuz.com/{track|album}/ID`
- `tidal.com/browse/{track|album}/ID`, `listen.tidal.com/…`
- `open.spotify.com/(intl-xx/){track|album}/ID`
- `discogs.com/{master|release}/ID` (release only — Discogs is album-only today)

**Open questions, for the ADR when this is picked up:**
- Interface shape: `IStreamingProvider` has `SearchAsync` and `LookupAsync` (by ISRC/UPC) today, but nothing that takes a provider id directly — this needs a new member (or a new interface) plus one URL parser per provider.
- Mapping a YouTube Music `MPREb_…` browse id (the id shown in the YT Music app/site URL) back to something the Data API can read.
- How resolve is reached without a cached search candidate id — today `POST /api/catalog/resolve` only takes `{ id }` from `ISearchCandidateCache`.
- Whether the YouTube `search.list` quota (point 9) needs a Google quota-extension request before this ships for real users.
- Whether a pasted exact item should be allowed to count as an exact YouTube match for its own page, given the known limitation above.

---

## Artist share pages

**Status:** idea.

**Problem.** `StreamingResultType` only has `Track` and `Release` — there's no artist kind anywhere in search, the catalog, or share pages. An artist link (pasted, see above) or an artist search result currently has nowhere to go.

Until this exists, a pasted artist link is reported as not supported yet (see the table in "Paste a link instead of searching" above).

---

## Scheduled catalog refresh

**Status:** on roadmap — see [roadmap.md](roadmap.md).

A background job that re-checks existing provider links over time (is the release/track still on the service?) and re-checks "not found" platforms more often (has it appeared since?), using the check dates [decisions/0014](decisions/0014-resolve-time-catalog-enrichment.md) already saves. Users keep getting the existing share page with no new provider calls; the catalog updates in the background. Needs a scheduler, which may settle 0014's open background-runner question.

---

## User-suggested links

**Status:** on roadmap — see [roadmap.md](roadmap.md).

A share-page viewer suggests a link instead of, or in addition to, an existing one; hodnota checks that the link is valid and fits the page before adding it. The "not found" row on the share page is the natural place for the button.

---

## Several versions per platform

**Status:** on roadmap — see [roadmap.md](roadmap.md).

Services often have the original, a remaster, and reissues of one release; the share page should show all of them. Real example: Metallica's *Load* has two official YouTube Music album playlists (`OLAK5uy_nn9_UEJTyKvU65JlglPBRMp9eWyxOTFWc` and `OLAK5uy_nf8K8yHuLThX0qvPGCIM2eBKndPale4vU`); until this item is done the page shows one of them. Needs the one-link-per-platform unique index from [decisions/0007](decisions/0007-catalog-data-model.md) to change.

---

## Catalog-first search

**Status:** on roadmap — see [roadmap.md](roadmap.md).

Search shows matching catalog items first, so users pick an existing item instead of searching the providers again and reaching the same share page.

---

## Discogs link on song pages

**Status:** idea.

A song's share page has no Discogs row, because Discogs is album-only. It could link to the master of the song's album. Deferred in [decisions/0014](decisions/0014-resolve-time-catalog-enrichment.md)'s "Out of scope" section.

---

## Split the provider trust order

**Status:** idea.

`ProviderTrustOrder` (`discogs, qobuz, tidal, spotify, deezer, youtube`) decides two things at once: whose metadata wins when providers disagree (artist spelling, release date), and which search rows come first. Splitting it into a catalog-authority order and a display order is deferred in [decisions/0014](decisions/0014-resolve-time-catalog-enrichment.md)'s "Metadata authority" and "Out of scope" sections. Working hypothesis from the original roadmap brief: Discogs first for release/artist metadata.

---

## Multiple UI/UX themes

**Status:** idea.

Dark, light, a classic MS-DOS-style theme, possibly more.

---

## Storefront/country selection per user

**Status:** idea.

Apple Music's search and links depend on a storefront country (`AppleMusic:Country`, see [decisions/0016](decisions/0016-apple-music-provider.md)); it is a single fixed config value (`US`) today. Pick it per user instead: from the request's IP address, or overridden by an explicit "I'm from" preference on the account. Likely useful for other providers too if a similar per-region setting shows up later.

---

## Compilation / "Various Artists" releases

**Status:** idea.

**Problem.** A compilation's tracks each have their own artist, but the release itself is credited to "Various Artists" (or a curator). The catalog has one artist per release, and search merging and enrichment match on artist + title. A compilation fits neither cleanly. Apple Music's song-grouped album search ([decisions/0016](decisions/0016-apple-music-provider.md)) also credits a compilation to the track's artist (`artistName`), not the release's own credit (`collectionArtistName`).

**Example.** An album search for "metallica enter sandman" on Apple Music can return a compilation that contains the track, credited to "Metallica" instead of "Various Artists" (not yet confirmed live).

**Settled.** Compilations get share pages. They are real releases people want to share, so dropping them from search is not an option.

**Open questions, for the ADR when this is picked up:**
- How a compilation is stored in the catalog: a shared "Various Artists" artist row (its auto-creation was deferred in [decisions/0007](decisions/0007-catalog-data-model.md)), no release-level artist, or per-track artists only.
- How it is matched across providers: artist + title is weak when the artist is "Various Artists" (each provider spells it differently, and many compilations share generic titles like "Greatest Hits"). UPC is the natural key, but Apple Music has no usable UPC lookup.
- Which artist search results show, and how `ReleaseType.Compilation` gets set (Deezer maps `compile` to it; Apple Music has no equivalent field).

---

## Localization support

**Status:** idea.

Localization at every layer: API, Web UI, mobile app.

---

## Move docs to some wiki and use a board insted ADR files

**Status:** idea.

**Problem.**
Not a problem, just looking for a best way in case solo project will have contributors.

---

## CSP content security policy

**Status:** idea.

**Problem.**
The CSP was out of focus. Must be a part of the UI implementation stage.

---