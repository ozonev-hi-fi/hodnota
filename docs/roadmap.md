# Roadmap

Epic-level plan, checked off as things get done. Task-level work doesn't belong here — this file only tracks the big steps; branch directly for it (see [decisions/0001](decisions/0001-branching-and-versioning-strategy.md) — no GitHub Issue required, one's only worth filing if you want extra tracking).

- [x] Come up with a name for the project (needs a brainstorming session). — kept `hodnota` as a working/draft name, see [decisions/0002-project-name.md](decisions/0002-project-name.md).
- [x] Create/rename the GitHub repository to match the name. - kept the same due to previous decision.
- [x] Design the detailed architecture of the first version (see [architecture.md](architecture.md)) — see [decisions/0003](decisions/0003-initial-architecture.md).
- [x] Scaffold the backend solution (Clean Architecture projects) and the `/web` React app — see [decisions/0004](decisions/0004-scaffold-backend-and-web-app.md).
- [x] Implement auth (Identity) — email + password via ASP.NET Core Identity, bearer tokens, PostgreSQL (prod and local dev container) — see [decisions/0005](decisions/0005-auth-identity.md).
- [x] Add OpenAPI generation + a dev-only interactive API UI (Scalar or similar, gated to Development, not shipped to prod) — see [decisions/0006](decisions/0006-openapi-scalar-dev-ui.md).
- [x] Implement the catalog data model + EF Core migrations (Postgres/SQLite) — see [decisions/0007](decisions/0007-catalog-data-model.md).
- [x] Add CI security/quality scanning (SAST, SCA, SBOM, license gate) — CodeQL, Dependency Review, CycloneDX SBOM, Dependabot — see [decisions/0008](decisions/0008-youtube-search-sharepage-skeleton.md). SonarCloud was dropped (advertised free tier requires payment details in practice); code smell/duplication coverage is an explicitly open gap. Repo-settings toggles (secret scanning, push protection, Dependabot alerts) still need manual follow-up (see ADR Consequences).
- [x] Implement auth UI (Web) — register/login/forgot-password/email-confirmation/change-password screens against the Identity API, the search/results page consuming `/api/catalog`, and the share page (folded in from `SharePage`'s backend-only landing in [decisions/0008](decisions/0008-youtube-search-sharepage-skeleton.md)) — see [decisions/0009](decisions/0009-auth-ui-search-share-web.md).
- [x] Implement a first streaming provider — YouTube search + SharePage creation, backend only (no UI) — see [decisions/0008](decisions/0008-youtube-search-sharepage-skeleton.md). Originally scoped as an "end-to-end walking skeleton"; descoped to API+DB only once in progress, with the UI half moved to the item above instead.
- [ ] Implement the remaining first-release providers
- [ ] Add observability: structured logging conventions + basic error/crash alerting — no dependency on the hosting choice below; a documentation gap surfaced by an SDLC audit, see [decisions/0010](decisions/0010-uat-production-readiness-gate.md).
- [ ] Choose and add a `LICENSE` file — before first real release/deployment, not before (repo is public now with no LICENSE, which defaults to "all rights reserved" — the safe side of the ambiguity, so no urgency). Goal is source-available but restricted against commercial use by others, not necessarily OSI-approved "open source" — plain AGPL-3.0 doesn't fit since it still permits commercial use (only requires sharing modifications). Candidates being weighed, decision still open: CC BY-NC-SA 4.0 (Creative Commons explicitly advises against using CC licenses for software — no patent handling, poor fit), PolyForm Noncommercial (built for source-available non-commercial software), Business Source License/BUSL (time-delimited commercial-use restriction that converts to a real OSS license later — used by MariaDB, Sentry, CockroachDB; may suit a scenario where investors/commercialization enter the picture later).
- [ ] Figure out hosting — must be free (or effectively free) to start.
- [ ] Backup & disaster recovery plan — depends on the hosting choice above; promotes the aspirational mention in [architecture.md](architecture.md)'s Hosting section into real, tested work, see [decisions/0010](decisions/0010-uat-production-readiness-gate.md).
- [ ] Integrate email service
- [ ] Use email service for auth confirmation flows — also a practical prerequisite for UAT itself (see below), since `RequireConfirmedAccount = true` blocks any invited tester from confirming an account without it.
- [ ] Define UAT / production-readiness gate — privacy policy published + human security review (incl. CSP, secure headers, secure cookie attributes if token storage ever moves off `localStorage`) as acceptance criteria before opening real public registration; see [decisions/0010](decisions/0010-uat-production-readiness-gate.md).
- [ ] Implement auth (Google/Facebook external login)
- [ ] Third-party streaming-API terms-of-service review (YouTube, Qobuz, Tidal, Deezer, Apple Music, Bandcamp) — deliberately after the UAT gate above, non-blocking for a non-commercial pre-release project, see [decisions/0010](decisions/0010-uat-production-readiness-gate.md).
- [ ] Sketch/scaffold the MAUI mobile app
- [ ] Implement auth UI (Mobile)
