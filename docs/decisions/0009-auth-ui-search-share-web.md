# 0009. Auth UI, search page, and share page (Web)

Status: accepted

## Context

[roadmap.md](../roadmap.md)'s next open item is "Implement auth UI (Web) — register/login screens
against the Identity API, plus the search/results pages consuming the `/api/catalog` endpoints."
Per the project owner, this task also folds in the Share page UI: `SharePage` persistence already
landed backend-only in [decisions/0008](0008-youtube-search-sharepage-skeleton.md), and there is no
separate roadmap line for its UI — it rides along with this item instead of waiting for one.

Several things this task depends on were deliberately left open by earlier ADRs:

- [decisions/0005](0005-auth-identity.md) fixed bearer tokens as the wire mechanism for every
  client, but explicitly deferred the web SPA's login UI, saying only that it would "hold the
  access token in memory (not `localStorage`)... CORS config for the SPA dev server is deferred to
  that step." This ADR is that step, and it revisits that specific detail (see Decision, token
  storage, below).
- [decisions/0008](0008-youtube-search-sharepage-skeleton.md) left both `CatalogController`
  endpoints (`search`, `resolve`) fully anonymous, named "`SharePage` ownership/auth" as a
  deliberate, temporary gap "because this pass is API-only with no web UI at all," and pointed at
  this exact roadmap item as the one that closes it. It also left no way to re-fetch an
  already-created `SharePage` by id — nothing needed one yet, since `resolve` both creates and
  returns it in a single call.
- [decisions/0006](0006-openapi-scalar-dev-ui.md) produced the OpenAPI document itself but left
  "typed client generation for `/web`" as an open question in [architecture.md](../architecture.md).
- [decisions/0004](0004-scaffold-backend-and-web-app.md) scaffolded `/web` as a bare Vite/React
  template with zero routing, CSS framework, HTTP client, or dev-proxy/CORS wiring — every one of
  those is a greenfield choice for this task.

This task is cross-cutting (tech-stack choices that affect future theming/localization/MAUI
parity, plus a security-relevant auth-storage choice) and touches decisions earlier ADRs
explicitly deferred, so it crosses the "worth an ADR" bar in [CLAUDE.md](../../CLAUDE.md) rather
than being silently re-derived from the code later.

## Decision

### Catalog auth: `search`/`resolve` require login; viewing an existing share page stays anonymous

`CatalogController` gets a class-level `[Authorize]` (fail-closed default for any future action
added to this controller), with `[AllowAnonymous]` only on the new action that fetches an
already-created `SharePage` by id (`GET /api/catalog/sharepages/{id}`). Requiring login to search
and to create a share page closes the gap [decisions/0008](0008-youtube-search-sharepage-skeleton.md)
named; but a page's whole purpose is to be shared with someone who opens the link cold, so viewing
one anonymously isn't a gap to close — it's the feature.

### Require a confirmed email before login; confirmation link points at the SPA, not the API

[decisions/0005](0005-auth-identity.md) set `RequireConfirmedAccount = false`, explicitly because
there was no web UI yet to confirm anything with — an unconfirmed user had no way to even see a
confirmation link. Now that `ConfirmEmailPage` and the rest of the auth UI exist, this ADR flips
it to `true`: `POST /api/auth/login` now rejects an unconfirmed account, distinguishably —
ASP.NET Identity returns `{"detail":"NotAllowed"}` for this case specifically, not the generic
`{"detail":"Failed"}` a wrong password produces, so `LoginPage` shows a specific "please confirm
your email" message rather than a generic login failure. `RegisterPage`'s post-registration
message changes to match: no longer "you can now log in" (no longer true), but "check your email
before you can log in."

Discovered while testing this manually: the confirmation link `NoOpEmailSender` logs pointed
directly at `Hodnota.Api`'s own `GET /api/auth/confirmEmail` endpoint — a bare API response, never
touching the SPA's `ConfirmEmailPage` at all. Originally scoped as an accepted limitation (see
`ConfirmEmailPage`'s design below), revisited once real friction surfaced: `NoOpEmailSender`
already owns what it logs, so it can rewrite the link instead of merely relaying it — parse
`userId`/`code`/`changedEmail` out of the API-pointing link handed to it and rebuild a link
pointing at `{WebApp:BaseUrl}/confirm-email` instead (`Hodnota.Infrastructure.Identity.WebAppConfiguration`,
defaulting to `http://localhost:5173` for local dev). This isn't just a local convenience — a real
future email sender should link to the SPA too, never to a bare API endpoint, so this is the
correct direction for that eventual replacement as well, not scope creep on this task.

### `SharePageLinkResponse` gains a platform-category field

The Share page UI needs to group links under headings ("Listen"/"Buy"/"Discover"), which is a
UI-copy decision. What kind of platform a link points to (`Platform.Type` /
`Hodnota.Domain.Catalog.PlatformType`) is a data-driven fact, not UI policy, so the backend
exposes it and the frontend owns the label mapping. `Hodnota.Contracts` gets its own
`PlatformType` enum mirroring the domain one (same split-and-map precedent already used for
`CandidateType`), rather than leaking the Domain type across the API boundary.

Discovered during implementation: the new `PlatformType` enum initially shipped without the
`[JsonConverter(typeof(JsonStringEnumConverter<PlatformType>))]` attribute `CandidateType` already
carries, so it serialized as a raw integer instead of a name. A global `JsonSerializerOptions`
registration in `Hodnota.Api` was tried as a fix and rejected — it only covers the API's own
serialization, not any other consumer deserializing these DTOs with its own default options
(this project's `HttpClient`-based tests, and eventually the MAUI mobile client, which shares
these same `Hodnota.Contracts` types). The per-type attribute is intrinsic to the type itself, so
it is correct for every consumer regardless of which `JsonSerializerOptions` they use. This is
now a standing rule for every `Hodnota.Contracts` enum, enforced by a reflection-based test
(`Hodnota.Api.Tests.Contracts.EnumJsonConverterTests`) rather than left to memory.

### Bootstrap 5, CSS only, no `react-bootstrap`

Grid, responsive layout, and form/button styling for free, and Bootstrap 5.3+'s CSS-variable
theming gives a natural seam for the manifesto's future dark/light/MS-DOS-style themes (not built
now). Plain classes on plain elements — no component-wrapper library — keeps the dependency
footprint small and the markup easy to replace once real visual design work happens. No JS bundle
is included: nothing in this task's scope (plain forms, a keyboard-driven list built in React
itself) needs Bootstrap's modal/dropdown/toast/collapse widgets.

### Typed API client: `openapi-typescript` + `openapi-fetch`, not a full generator

Resolves [decisions/0006](0006-openapi-scalar-dev-ui.md)'s open question. `openapi-typescript`
turns `/openapi/v1.json` into TypeScript types only (no generated request code to maintain), and
`openapi-fetch` is a small typed wrapper around `fetch` driven by those types. This is picked over
a heavier full-client generator (orval, NSwag, openapi-generator-cli) for the smallest footprint
and the easiest path to ripping it out later, matching the project's "simplest possible design"
bias for this pass. Flagged as a risk going in: `MapIdentityApi`'s built-in OpenAPI metadata has
historically under-typed some request/response bodies as bare `object`, which would have needed a
small hand-written `identity-types.ts` fallback for just those operations. Verified once the
document was actually generated: every Identity operation used here (`register`, `login`,
`refresh`, `forgotPassword`, `resetPassword`, `confirmEmail`, `manage/info`) came through fully
and concretely typed — no fallback file was needed.

### Token storage: `localStorage`, superseding [decisions/0005](0005-auth-identity.md)'s in-memory suggestion

Both the access and refresh tokens are stored in `localStorage`, behind one small
`tokenStorage.ts` module that is the only code allowed to touch it. This is a deliberate reversal
of the "in memory (not `localStorage`)" line in [decisions/0005](0005-auth-identity.md) — not an
oversight. `localStorage` is simpler to implement for a first pass (no silent-refresh-on-reload
flow needed) and matches this project's established walking-skeleton discipline elsewhere
(`NoOpEmailSender`, `IMemoryCache` for search candidates): accept the simplest working thing now,
revisit if/when a real security-hardening pass is scheduled. Isolating all access behind one
module keeps that future change a single-file swap instead of a scattered one.

### No CORS; a Vite dev-server proxy instead

`web/vite.config.ts` proxies `/api/*` to the backend's dev URL, so the browser sees one origin in
dev the same way it will in a same-origin `wwwroot`-hosted production deployment
([architecture.md](../architecture.md)'s Components section). No `AddCors`/`UseCors` is added to
`Hodnota.Api` — there is nothing for a CORS policy to permit once nothing cross-origin exists.
Production SPA static-file wiring (`UseSpaStaticFiles`/`MapFallbackToFile`) remains a separate,
pre-existing open item; nothing in this task needs it; since verification runs the Vite dev server
against the API's own dev server.

### Layout: one centered column, no app-shell

A single `CenteredLayout` wrapper centers all page content in one column. No header, footer,
sidebar, or navigation component is built for this pass — matches the walking-skeleton scope
[decisions/0008](0008-youtube-search-sharepage-skeleton.md) already established, deferring real
visual/navigation design to later, dedicated work.

## Consequences

- [architecture.md](../architecture.md) is updated: the Authentication & Authorization section
  gains the catalog-auth model, the `localStorage` choice (with its explicit reversal of
  [decisions/0005](0005-auth-identity.md)'s suggestion), and the `RequireConfirmedAccount = true`
  reversal of that same ADR; the Components section's Web UI line names the new stack (React
  Router, Bootstrap 5, openapi-typescript/openapi-fetch); the "Typed client generation for `/web`"
  line is removed from Open Questions.
- Every existing test that registered then immediately logged in without confirming (backend
  integration tests, the walking-skeleton's own auth flow) needed a way to confirm first. Rather
  than reimplementing ASP.NET Identity's internal confirmation-token encoding by hand in test
  code (tried, and got the encoding wrong — a real correctness trap, not just extra code), the
  fix is a shared `CapturingEmailSender` test double (`Hodnota.Api.Tests.Identity`) that stores
  the exact link `MapIdentityApi` hands to `IEmailSender<ApplicationUser>`, which tests then hit
  directly — exercising the real `confirmEmail` endpoint without guessing its token format.
- [roadmap.md](../roadmap.md)'s "Implement auth UI (Web)" item is checked off, expanded to note
  the share-page UI folded in.
- Deliberately deferred, each a named future item rather than scope creep here: provider link
  icons/logos (text-only links for this pass), `SharePage` link reordering/hiding UI, an
  account/profile page beyond changing a password, dark/light/MS-DOS theming, localization,
  mobile app parity, SPA production static-file hosting wiring.
- Running the web app now requires `npm install` of a handful of new dependencies
  (`react-router`, `bootstrap`, `openapi-fetch`, `openapi-typescript`) — a step up from the
  previous zero-dependency scaffold, accepted for the functionality they unlock.

## Also in this branch: npm exact version pinning

Discovered while installing the dependencies above: `web/package.json` used semver-caret ranges
(`^x.y.z`, one `~x.y.z`), unlike `Directory.Packages.props`'s exact pins on the .NET side. This
is a project-wide npm convention, not scoped to this feature, so it's recorded here rather than
re-litigated per install. `web/package.json` now pins every dependency to its exact resolved
version, and `web/.npmrc` sets `save-exact=true` so `npm install <pkg>` writes exact versions by
default going forward — no need to remember `--save-exact`. This refines
[decisions/0003](0003-initial-architecture.md)'s "track latest, upgrade promptly" policy: that
policy is about upgrade cadence, not about letting a caret range silently float to whatever's
newest when an unrelated `npm install` re-resolves the tree (observed firsthand during this
task — installing one new package shifted several already-pinned packages' resolved versions).
Dependabot version updates (already adopted, see
[decisions/0008](0008-youtube-search-sharepage-skeleton.md)) deliver "upgrade promptly" as a
reviewable per-bump PR instead.

## Also in this branch: a second, agent-only local dev database

Discovered during manual verification of the flows above: requiring a confirmed email before
login (see the Decision above) meant every register→login pass through `run-hodnota` left a real
row in the local dev Postgres database, on top of rows from earlier manual runs. The original
cleanup advice (`docker compose down -v && docker compose up -d`) wipes the whole named volume —
acceptable when the database held nothing but disposable test data, but not once a developer
starts keeping their own persistent dataset there (e.g. one already-registered user reused across
manual page checks, instead of registering a new one every time).

The container now provisions two databases on first init (`docker/postgres-init/
create-agent-database.sql`, which — like `POSTGRES_DB` itself — only runs against a brand-new
volume): `hodnota` (the developer's own, left alone by tooling) and `hodnota_agent` (agent/script
scratch space). Agent-driven runs (`run-agent-api.sh`, `smoke.sh`) point at `hodnota_agent` via a
`ConnectionStrings__Default` environment-variable override, which wins over both `.env` files
without editing either — `Hodnota.Infrastructure.DotEnvLoader` already loads `.env`/`.env.local`
with NoClobber semantics (a real process env var is never overwritten), so this needed no new
config-loading mechanism, `.env.local` value, or .NET User Secrets. Cleanup is now scoped to
dropping and recreating just `hodnota_agent` (see the `run-hodnota` skill) — `docker compose down
-v` must never be used for this anymore.
