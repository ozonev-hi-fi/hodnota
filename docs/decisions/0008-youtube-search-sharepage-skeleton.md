# 0008. YouTube search + SharePage walking skeleton

Status: accepted

## Context

[roadmap.md](../roadmap.md)'s next open item is "Implement a first streaming provider end-to-end as a walking skeleton." [decisions/0003](0003-initial-architecture.md) and [decisions/0007](0007-catalog-data-model.md) both explicitly deferred `SharePage`'s schema to this exact item — its shape wasn't forced by any concrete requirement until now, and `Hodnota.Application` was left empty of catalog use-case/repository interfaces for the same reason: guessing the access pattern against zero callers risked designing the wrong interface before the real one (intertwined with `IStreamingProvider` search, not plain CRUD) was known.

The requested shape is two endpoints:

1. Search — the caller sends free text (an artist, album, or song name); the API searches YouTube and returns 5–10 lightweight candidates (`type`, `name`, `artist`, `imageUrl`, `id`), held temporarily server-side rather than persisted.
2. Resolve — the caller sends one candidate's `id`; the API fetches full detail from YouTube, fills the catalog (`Artist`/`Release`/`Track`/`ProviderLink`) as a side effect, creates a `SharePage`, and returns it with both a YouTube and a YouTube Music link.

This is deliberately a skeleton: no SharePage customization (reordering/hiding links), no web UI, and only the simplest dedup pass on catalog fill. Each is a named follow-up, not solved here.

## Decision

### YouTube Data API v3 as the sole search source; both links are one derivation, not two searches

The official YouTube Data API v3 `search.list` is the only data source. There is no separate YouTube Music search API to call. Both displayed links come from the *same* search result via a uniform domain swap (`www.youtube.com` → `music.youtube.com`, identical path and query), which YouTube Music genuinely honors for both cases search can return:

- Track (`search.list` `type=video`): YouTube = `https://www.youtube.com/watch?v={videoId}`, YouTube Music = `https://music.youtube.com/watch?v={videoId}`.
- Release (`search.list` `type=playlist`, including YouTube's auto-generated full-album playlists, `OLAK5uy_...`-prefixed IDs): YouTube = `https://www.youtube.com/playlist?list={playlistId}`, YouTube Music = `https://music.youtube.com/playlist?list={playlistId}`.

Modeling it as "search once, derive two links" is not a workaround, it's the correct model of how these two surfaces relate, since YouTube API doesn't split searches between YouTube and YouTube Music. `search.list`'s own response already carries everything this skeleton needs (title, channel name as a best-effort artist, thumbnail) — no follow-up `videos.list`/`playlists.list` call for extra detail, and resolve reuses the cached candidate's own data rather than calling YouTube a second time.

**Client mechanism: the official `Google.Apis.YouTube.v3` SDK, not a raw `HttpClient`.** This refines, not reverses, [decisions/0003](0003-initial-architecture.md)'s provider-abstraction language ("its own named `HttpClient` via `IHttpClientFactory`"): `IStreamingProvider` is the fixed abstraction/port that ADR fixes; what a concrete provider does *internally* to talk to its service is that provider's own implementation choice — an official free SDK when the vendor provides one, a raw `HttpClient` otherwise. `YouTubeStreamingProvider` is registered against an injected `YouTubeService` (`Google.Apis.YouTube.v3`), itself registered as a DI singleton (`Hodnota.Infrastructure.DependencyInjection.AddCatalog`) so its internal HTTP handler is reused rather than recreated per request — no bridge to ASP.NET Core's `IHttpClientFactory` is needed, since `YouTubeService` manages its own `Google.Apis.Http.ConfigurableHttpClient` internally. The accepted cost is dependency weight (`Google.Apis`/`Google.Apis.Auth` and their transitive OAuth-related dependencies, unused since this pass only needs API-key auth); the win is no hand-rolled JSON response models and an SDK that stays current with the API on its own.

**Quota**: the free tier is 10,000 units/day; `search.list` costs 100 units/call (~100 searches/day). Flagged as a known v1 constraint; not solved here — no caching-of-searches or quota-management strategy is built for this pass.

**API key storage**: a new `YouTube:ApiKey` config key, following [decisions/0005](0005-auth-identity.md)'s `DatabaseConfiguration`-style `public static class` of `const string` config-key literals convention (colocated with the code that owns it). Unlike the committed root `.env` (Postgres credentials only — not a real secret, per [architecture.md](../architecture.md), since it's localhost-only and never shipped), the YouTube key is a genuine secret and goes in `.env.local` only (already gitignored, per-developer). `Hodnota.Infrastructure.DotEnvLoader` already loads `.env` with `NoClobber` before `.env.local`, so a real deployment/CI environment variable set independently of either file always wins — no code change is needed later to wire a GitHub Actions secret through to whatever hosting platform gets chosen. Concrete deploy-time secret storage stays deferred to the still-open "hosting" and "secrets/config management for provider API keys" items in [architecture.md](../architecture.md); this ADR only settles local dev.

### Temporary candidate storage: in-process `IMemoryCache`

Search results are held server-side, keyed by a generated candidate `Id` (`Guid`), with a short TTL (~10–15 minutes) — long enough for a user to pick a result, short enough not to accumulate unbounded memory. No Redis or other distributed cache exists anywhere in this codebase yet (confirmed greenfield), and nothing about a single-instance dev/early-production deployment needs cross-instance sharing. `IMemoryCache` is the YAGNI-consistent choice matching this project's established discipline (see [decisions/0007](0007-catalog-data-model.md)'s JSONB/metadata-column rejection for the same reasoning pattern); revisit if/when hosting requires multiple API instances behind a load balancer.

### `SharePage`/`SharePageLink`: the first real schema, per [decisions/0003](0003-initial-architecture.md)'s sketch

[decisions/0003](0003-initial-architecture.md) sketched `SharePage` as referencing "a canonical catalog entity... carries its own filtered/ordered list of provider links to display — a page owner can exclude a link even if the catalog has it." This ADR gives that sketch its first concrete shape, in `Hodnota.Domain.Catalog` alongside the existing entities from [decisions/0007](0007-catalog-data-model.md):

- `SharePage`: `Id` (`Guid`), `ArtistId`/`ReleaseId`/`TrackId` (nullable FKs — the same "exactly one target set" CHECK-constraint-plus-three-partial-unique-indexes pattern `ProviderLink`/`EntityGenre` already established isn't needed here for *uniqueness* — a catalog entity can have more than one `SharePage` — but the "exactly one of three" CHECK constraint itself still applies, since a `SharePage` must reference exactly one kind of catalog entity), `UserId` (nullable — see "Anonymous endpoints" below), implementing `IHasTimestamps`.
- `SharePageLink`: a join table between `SharePage` and `ProviderLink`, carrying `DisplayOrder` (`int`) and `IsVisible` (`bool`, default `true`) — this is the "page owner can exclude a link even if the catalog has it" mechanism, without copying link data onto the page itself. Pure join table, no independent lifecycle, so it does not implement `IHasTimestamps` — matching the `ReleaseTrack`/`ArtistCredit`/`EntityGenre` precedent from [decisions/0007](0007-catalog-data-model.md).

No endpoints exist yet to reorder or hide a `SharePageLink` — this pass only creates the schema and auto-populates it (every `ProviderLink` produced for the resolved entity gets a `SharePageLink`, `IsVisible = true`, ordered by creation). Customization is explicit future work.

### Catalog fill with exact-match dedup via `ProviderLink`, not fuzzy matching

On resolve, before inserting a new `Artist`/`Release`/`Track`, look up an existing `ProviderLink` by `(PlatformId = YouTube's seeded Platform.Id, ExternalId = videoId or playlistId)`. If one exists, reuse its linked catalog entity instead of creating a duplicate; only create new `Artist`/`Release`/`Track`/`ProviderLink` rows when no such link exists yet. This is a single exact-key lookup — it is not name/artist fuzzy matching, and it does not consult `Upc`/`Isrc` (YouTube search results don't expose those). Broader dedup — matching a new provider's result against an existing catalog entity that has no YouTube link at all, or searching the catalog in parallel with live provider search — is named future work, not this pass's problem: it needs a real matching strategy (which fields, what confidence threshold) that doesn't exist yet, and would risk merging two genuinely different tracks that happen to share a name.

This exact-key lookup is check-then-act, so it's enforced at the database level too, not just relied on: `ProviderLink` gets a fourth unique index, `(PlatformId, ExternalId)`, alongside the three from [decisions/0007](0007-catalog-data-model.md) — a platform's own identifier for an entity is unique regardless of which catalog entity it ends up attached to. `EfCatalogRepository.CreateSharePageAsync` retries once on `DbUpdateException` (clearing the change tracker first): if two concurrent resolves for the same not-yet-seen external ID race, the loser's insert trips the unique index instead of creating a duplicate, and the retry's lookup finds the winner's now-committed row and reuses it.

### Anonymous endpoints; ownership deferred

Both endpoints are anonymous — no auth requirement, no `SharePage.UserId` populated. This is not because ownership is unimportant ([decisions/0003](0003-initial-architecture.md) already frames `SharePage` as "created by a user"), but because this pass is API-only with no web UI at all: the next roadmap item, "Implement auth UI (Web)," is a separate future ADR that pairs login/register pages with the search/results web pages this API exists to serve. Wiring real ownership into an endpoint with no logged-in caller to attach it to would be premature. `SharePage.UserId` stays nullable now and gets populated once that feature lands; this is a deliberate, temporary, and named gap, not an oversight.

### Endpoint style: MVC Controllers for hand-written endpoints

The only endpoints in the codebase today (`MapIdentityApi<ApplicationUser>()`, mounted at `/api/auth`) come from a sealed ASP.NET Core Identity library extension method that always emits minimal-API endpoints, regardless of what convention this project picks for its own code — there is no existing hand-written-endpoint precedent to either follow or break. New, hand-written endpoints use MVC Controllers (`[ApiController]`, attribute routing) instead of minimal-API `MapGroup`/route-handler lambdas: `[ApiController]`'s automatic model-validation/`400` responses and the familiar one-class-per-resource shape scale better as more endpoints land (the walking skeleton's own two actions, then auth UI's needs, then the remaining streaming providers), and Controllers work identically well with the existing `Microsoft.AspNetCore.OpenApi`/Scalar setup from [decisions/0006](0006-openapi-scalar-dev-ui.md). `/api/auth` remains minimal-API-shaped since that's fixed by the framework; this decision governs every future hand-written endpoint, not just this feature's two.

## Consequences

- Deliberately deferred, each its own future roadmap/ADR item: fuzzy/cross-provider catalog dedup, `SharePage` link customization (reordering/hiding), any web UI, `SharePage` ownership/auth, concrete deploy-time secret storage for provider API keys.
- Establishes `IStreamingProvider` (per [decisions/0003](0003-initial-architecture.md)'s original sketch, added to `Hodnota.Application` for the first time) with its first real implementation, `Hodnota.Infrastructure.Providers.YouTube`, and clarifies that ADR's provider-abstraction wording: the per-provider HTTP/SDK mechanism is an implementation choice, not mandated to be a raw `HttpClient`.
- Establishes MVC Controllers as the convention for all future hand-written API endpoints.
- `SharePage`/`SharePageLink` join the catalog schema in `Hodnota.Infrastructure/Catalog/Configurations/`, migrated via the existing single `ApplicationDbContext`/`Hodnota.Infrastructure/Migrations/` history — no new `DbContext`, matching [decisions/0007](0007-catalog-data-model.md)'s precedent. Needs the same SQLite fast-test-plus-Testcontainers-Postgres integration-test pass as any schema-changing feature.
- [architecture.md](../architecture.md)'s Data Model and Open Questions sections are updated once this is implemented, and [roadmap.md](../roadmap.md)'s walking-skeleton checkbox is checked off.

## Also in this branch: CI security/quality scanning

A separate, unrelated roadmap item — "Add CI security/quality scanning (SAST, SCA, SBOM, license gate)" — was drafted earlier (`docs/decisions/drafts/ci-security-scanning.md`, on its own branch) and stashed with uncommitted WIP. Rather than resurrect a separate branch for it, it's folded into this one and recorded here instead of as its own numbered ADR, since this branch had already touched a large fraction of the codebase and the draft was ready to implement.

### Context

Catch vulnerabilities, code smells, duplication, likely bugs, and dependency risk automatically in CI, plus produce CycloneDX SBOMs and a lightweight license/legal-clearance signal — all on tooling that's free and won't get pulled out from under the project later. SonarQube, Snyk, and CycloneDX were named as known tools, not mandated choices.

Repo facts, re-confirmed at implementation time (2026-09-06):
- Repo is **public** (`ozonev-hi-fi/hodnota`, confirmed via `gh repo view`) — this is what makes CodeQL, Dependabot, and secret scanning free with no usage caps. If the repo ever goes private, this whole stack needs re-evaluation (GitHub Advanced Security becomes paid for private repos).
- Stack: .NET 10 backend (`Directory.Packages.props`, `global.json` pinned to `10.0.400`) + React/Vite/TS web app under `/web` (npm, Biome).
- Existing CI convention ([decisions/0004](0004-scaffold-backend-and-web-app.md)): two independent, path-filtered workflows (`ci-backend.yml`, `ci-web.yml`), advisory only (no required status checks/branch protection yet).
- No `LICENSE` file exists yet at repo root — the project's own license is a separate open question, out of scope here.

### Decision

| Need | Tool | Why this one |
|---|---|---|
| Vulnerabilities + likely bugs (SAST) | **CodeQL** (`github/codeql-action`) | Free forever for public repos, GitHub-native, covers C# and JS/TS, deep taint-tracking analysis. |
| Dependency vulnerabilities | **Dependabot alerts** (repo setting) + **Dependency Review Action** (PR-diff gate) | Both free/uncapped for public repos, GitHub-native, zero extra accounts. |
| Dependency version currency | **Dependabot version updates** (`.github/dependabot.yml`) | Free, native, opens PRs itself — no workflow needed. |
| SBOM | **CycloneDX** — `CycloneDX` dotnet tool for backend, `@cyclonedx/cyclonedx-npm` for web | Explicitly requested; both are the official CycloneDX-org tools for their ecosystem, free/OSS, no account needed. |
| Legal/license clearance | **Dependency Review Action's license allow/deny list** (PR gate) + the **CycloneDX SBOM's license fields** (standing inventory) | Reuses tools already in the stack instead of adding a dedicated (usually paid) legal tool — "good enough" clearance for a solo pre-1.0 project. |
| Secret leaks | **GitHub secret scanning + push protection** (repo setting) | Free for public repos, zero config, directly serves "find vulnerabilities." Not explicitly requested but a zero-cost, obvious gap otherwise. |

**Code smells, duplication, and maintainability metrics are explicitly not covered** — see "Rejected alternatives" below for why SonarCloud, this need's original candidate, doesn't hold up, and why nothing replaces it in this pass.

Workflow shape: `codeql.yml` (language matrix, not path-filtered, matches GitHub's default-setup shape), `dependency-review.yml` (PR-only, severity threshold + copyleft license deny-list), `sbom.yml` (push to develop/main + manual dispatch, uploads CycloneDX SBOMs as artifacts), and `.github/dependabot.yml`.

Repo settings (secret scanning, push protection, Dependabot alerts) are toggled via `gh api` rather than the GitHub UI, per the original draft's pre-authorization — this needs a `gh` session with admin rights on the repo (see `CLAUDE.local.md` to check if it contains machine's account-switching setup, not relevant to other environments):

```
gh api -X PATCH repos/ozonev-hi-fi/hodnota --input - <<'EOF'
{
  "security_and_analysis": {
    "secret_scanning": { "status": "enabled" },
    "secret_scanning_push_protection": { "status": "enabled" }
  }
}
EOF
gh api -X PUT repos/ozonev-hi-fi/hodnota/vulnerability-alerts
gh api -X PUT repos/ozonev-hi-fi/hodnota/automated-security-fixes
```

### Public-repo dependency — check this first if visibility ever changes

Every tool above was picked *because* it's free for a public repo. If `ozonev-hi-fi/hodnota` is ever flipped to private, walk this table before assuming the CI stack still works as designed:

| Tool | Public-repo-gated? | What happens if the repo goes private |
|---|---|---|
| CodeQL | **Yes** | Requires GitHub Advanced Security (paid per committer) on a private repo. Needs replacing (e.g. self-hosted CodeQL CLI in a workflow, or drop to a different free SAST) or budget approval. |
| Secret scanning + push protection | **Yes** | Same GHAS gate as CodeQL. Free open-source alternative if needed: `gitleaks` or `trufflehog` as a workflow step. |
| Dependency Review Action | **Yes** | Requires the Dependency graph + GHAS on private repos. Replace with `osv-scanner` (fully free, no visibility gate) run against lockfiles as a PR step. |
| Dependabot alerts + version updates | No | Free on GitHub regardless of repo visibility — no action needed if the repo goes private. |
| CycloneDX SBOM generation | No | Both are local CLI tools running inside the workflow, not a hosted service gated by repo visibility — unaffected either way. |

A private-repo pivot doesn't quietly degrade this setup — it visibly breaks CodeQL/secret-scanning/Dependency-Review (workflows fail or GitHub disables the feature), which is the trigger to come back here and swap in the free-regardless-of-visibility alternatives named above.

### Consequences (CI scanning)

- Depends on the repo staying public — see "Public-repo dependency" above for exactly which tools break and what to replace them with if that ever changes.
- **Rejected alternatives, with reasons:**
  - **SonarCloud** — the draft's original pick for code smells/duplication/maintainability, based on an advertised free-for-public-repos tier. Re-verified at implementation time: signing up prompts for payment details with an auto-charge-after-trial structure in practice, regardless of a nominally free "OSS plan" existing somewhere in Sonar's plan matrix — exactly the "gets pulled out from under the project later" failure mode this whole exercise was trying to avoid, same category as Snyk below. No replacement tool is substituted in this pass — see the uncovered-need note above.
  - **Snyk** — free tier is usage-capped (limited tests/month); equivalent coverage is already free and uncapped for a public repo via CodeQL + Dependabot + Dependency Review. A capped free tier is exactly the "not future proof" failure mode this was trying to avoid.
  - **Self-hosted SonarQube Community Edition** — needs hosting infra, and hosting itself is still an open question in [architecture.md](../architecture.md). Revisit only if the repo goes private, or the CI budget/hosting picture changes enough to make self-hosting worthwhile.
  - **FOSSA / dedicated license-compliance SaaS** — paid, overkill for a pre-1.0 solo project. The Dependency Review license gate + CycloneDX SBOM license data is enough for now.
  - **OSV-Scanner, jscpd, ORT** — real tools, but redundant with what CodeQL/Dependabot already cover, or (for `jscpd`'s duplication detection specifically) not yet justified by a demonstrated problem — matching this project's established YAGNI discipline (see [decisions/0007](0007-catalog-data-model.md)). Candidates if a genuine coverage gap shows up later, `jscpd` foremost since duplication detection is the one need nothing above actually covers.
- [roadmap.md](../roadmap.md)'s CI-scanning checkbox is checked off; the draft file this section absorbs is deleted.
