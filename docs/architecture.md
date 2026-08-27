# Architecture

This is the living design document for the project. Unlike [MANIFESTO.md](MANIFESTO.md), which is frozen, this file should be kept in sync with reality as decisions are made or revised. When a choice is worth explaining *why*, capture it as an entry under [decisions/](decisions/) and link it from here instead of re-arguing it in place.

Status: v1 architecture designed — see [decisions/0003](decisions/0003-initial-architecture.md) for the first real design pass. Detailed schema, provider-specific implementations, and hosting are still open (see below).

## Components

- **API** — `Hodnota.Api`, ASP.NET Core, Clean Architecture (`Api` → `Infrastructure` → `Application` → `Domain`; `Domain` has no dependencies). Owns the database, business logic, and integration with third-party streaming services. Also hosts the built Web UI in production. Reasoning: [decisions/0003](decisions/0003-initial-architecture.md).
- **Web UI** — `/web`, React + Vite + TypeScript. Independent dev server (proxied to the API); built into the API's `wwwroot` for production so the app ships as one deployable unit (`UseSpaStaticFiles`/`MapFallbackToFile`).
- **Mobile app** — `Hodnota.Mobile`, .NET MAUI, sketched but not yet built. Lives under `/src` alongside the other .NET projects (same toolchain/solution), unlike `/web`. Talks to the same API (no separate backend), sharing DTOs via `Hodnota.Contracts`. Partially mirrors the Web UI, but its core purpose is share-sheet integration: a user shares an album/artist/song from a streaming app, picks this app from the share sheet, it finds or creates the corresponding sharing page, and from there the user shares a link to *that* page onward (to a messenger, etc.).
- **Satellite services** (later, out of scope for v1) — additional services built on top of the same core, e.g. a Telegram bot that watches for streaming links in a chat and replies with a page collecting the equivalent links across other services. A third-party-facing API may be needed to support these.

## Streaming-provider integration

Each supported service (see list below) implements a shared `IStreamingProvider` interface (search, match, resolve-by-URL) in its own class under `Hodnota.Infrastructure.Providers.<Service>`, with its own auth and rate-limit handling. An Application-layer aggregator fans a request out to all registered providers. Reasoning and interface shape: [decisions/0003](decisions/0003-initial-architecture.md).

## Authentication & Authorization

ASP.NET Core Identity (PostgreSQL-backed): email + password, Google, Facebook. Possibly Apple Sign-In later. Cookie-vs-JWT token strategy for the SPA/mobile/third-party clients is still open — see [decisions/0003](decisions/0003-initial-architecture.md).

## Hosting

Needs to be free (or effectively free) to start. Candidates to evaluate: containerization, Azure's free tier(s). Must also consider scalability if the product gets traction, and baseline security posture (attack surface, data leak prevention, a kill switch, disaster-recovery scenarios kept up to date).

## Database

EF Core. PostgreSQL in production, SQLite for local dev and fast tests. Schema-changing features need an integration-test pass against real PostgreSQL before merging (see [decisions/0003](decisions/0003-initial-architecture.md) for why SQLite alone isn't sufficient sign-off).

## Data Model (rough)

A persisted catalog of `Artist`/`Album`/`Track` entities plus `ProviderLink`s, built up as a side effect of the searches needed to construct sharing pages — intended to be reusable by future, unrelated projects (a "Music Wikipedia"). `SharePage` references a catalog entity and carries its own filtered/ordered list of provider links. Detailed schema is implementation-time work.

## Tooling & Conventions

Latest-stable-everything version policy; `hodnota.slnx` at repo root with one central `Directory.Packages.props` for every .NET project; root `.editorconfig` + .NET analyzers for C#, Biome for the web app; xUnit/AwesomeAssertions/NSubstitute for unit tests, `WebApplicationFactory`+Testcontainers(PostgreSQL) for integration, Playwright for E2E once there's a UI to exercise. Web component/unit tests use Vitest + React Testing Library (decided in [decisions/0004](decisions/0004-scaffold-backend-and-web-app.md), since 0003 didn't cover a JS test framework). Full reasoning: [decisions/0003](decisions/0003-initial-architecture.md).

## Supported Streaming Services — First Release (priority order)

1. YouTube + YouTube Music
2. Qobuz
3. Tidal
4. Deezer
5. Apple Music

### Purchase Platforms

- Bandcamp

## UX Notes

- Multiple themes: dark, light, a classic MS-DOS-style theme, possibly more.
- Localization at every layer: API, Web UI, mobile app.
- Screens: home/search, create/edit/view sharing page, user profile with a list of the user's pages.

## Branching & Versioning Strategy

Not strictly "architecture," but decided and settled, so it lives here rather than as an open item on the roadmap. Reasoning: [decisions/0001-branching-and-versioning-strategy.md](decisions/0001-branching-and-versioning-strategy.md). Day-to-day commands: [workflow.md](workflow.md).

- **`main`** — always deployable. Advances only via merge (from `develop` directly for now, from `release/*` once that exists) plus a SemVer tag (`vX.Y.Z`) on every merge, starting pre-1.0 (`v0.x.y`). No direct commits.
- **`develop`** — integration branch and default base for new work. Represents the current "alpha" state: everything intended for the next release, integrated, but not yet hardened or tagged.
- **`feature/<identifier>-<short-description>`** — branched off `develop`; `<identifier>` is the relevant ADR number when the task has one, otherwise omitted (no GitHub Issue required). Merged back into `develop` only once the feature is fully done: DB changes, business logic, and test coverage all in.
- **`release/*`, `hotfix/*`** — deferred, not created yet. To be added when actually needed:
  - `release/*` — cut from `develop` when preparing an actual release; stabilization only; merges to both `main` (tag) and back to `develop`.
  - `hotfix/*` — cut from `main` for urgent production fixes; merges to both `main` (patch tag) and `develop`.

## Open Questions

- Detailed EF Core schema/migrations.
- Provider-specific auth and implementation details, per streaming service.
- SPA build glue (Dockerfile or build script) to produce the single deployable artifact.
- Auth token strategy (cookie vs. JWT) for the SPA/mobile/third-party clients.
- Mobile app auth approach for a native client.
- Hosting provider/free-tier specifics not yet evaluated.
- CI pipeline (build/test/lint on every PR).
- Secrets/config management for provider API keys (depends on the hosting choice).
- Structured logging/observability approach.
- OpenAPI generation for typed clients.
- Dev container/Docker setup for onboarding consistency.
- i18n tooling for the manifesto's localization goal.
