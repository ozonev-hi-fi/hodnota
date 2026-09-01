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

ASP.NET Core Identity: email + password is implemented (`Hodnota.Infrastructure.Identity`'s `ApplicationDbContext`/`ApplicationUser : IdentityUser<Guid>`, mounted via `MapIdentityApi<ApplicationUser>()` under `/api/auth`), bearer tokens for every client (SPA, future mobile, future third-party API) rather than cookies. Google, Facebook, and possibly Apple Sign-In later remain a separate, not-yet-implemented roadmap item. Email confirmation is disabled for v1 (a `NoOpEmailSender` logs links instead of sending them) until the "Integrate email service" roadmap item lands. Reasoning: [decisions/0005](decisions/0005-auth-identity.md).

## Hosting

Needs to be free (or effectively free) to start. Candidates to evaluate: containerization, Azure's free tier(s). Must also consider scalability if the product gets traction, and baseline security posture (attack surface, data leak prevention, a kill switch, disaster-recovery scenarios kept up to date).

## Database

EF Core. PostgreSQL in production **and for local interactive dev** — a `docker-compose.yml` at repo root runs a local Postgres container with a persistent named volume, so `dotnet run`ning the API locally gets a stable dataset and the same versioned migrations used in prod, rather than a separate SQLite schema story. SQLite is scoped to just the fast, Docker-free automated test suites (`Hodnota.Api.Tests`, `Hodnota.Infrastructure.Tests`), which configure it directly and use `Database.EnsureCreated()` rather than migrations. Schema-changing features additionally need an integration-test pass against real PostgreSQL before merging — via Testcontainers, in a dedicated `*.IntegrationTests` project, kept separate from the persistent local dev container (see [decisions/0005](decisions/0005-auth-identity.md), refining [decisions/0003](decisions/0003-initial-architecture.md) for why SQLite alone isn't sufficient sign-off).

The root `.env` file is the single source of truth for the local dev Postgres credentials — intentionally committed (not a real secret: localhost-only, never shipped anywhere), consumed by both `docker-compose.yml` and `Hodnota.Api`/`dotnet-ef` (via `Hodnota.Infrastructure.DotEnvLoader`), so a fresh clone runs with zero manual setup. An optional, always-gitignored `.env.local` overrides it for a personal value. See [decisions/0005](decisions/0005-auth-identity.md).

## Data Model (rough)

A persisted catalog of `Artist`/`Album`/`Track` entities plus `ProviderLink`s, built up as a side effect of the searches needed to construct sharing pages — intended to be reusable by future, unrelated projects (a "Music Wikipedia"). `SharePage` references a catalog entity and carries its own filtered/ordered list of provider links. Detailed schema is implementation-time work.

## Tooling & Conventions

Latest-stable-everything version policy; `hodnota.slnx` at repo root with one central `Directory.Packages.props` for every .NET project; root `.editorconfig` + .NET analyzers for C#, Biome for the web app; xUnit/AwesomeAssertions/NSubstitute for unit tests, `WebApplicationFactory`+Testcontainers(PostgreSQL) for integration, Playwright for E2E once there's a UI to exercise. Web component/unit tests use Vitest + React Testing Library (decided in [decisions/0004](decisions/0004-scaffold-backend-and-web-app.md), since 0003 didn't cover a JS test framework). Full reasoning: [decisions/0003](decisions/0003-initial-architecture.md).

**Config key/value literals**: a repeated config key (e.g. `"Database:Provider"`) or one of its possible values (e.g. `"Postgres"`/`"Sqlite"`) belongs in a `public static class` of `const string` fields, colocated with the code that owns that config (e.g. `Hodnota.Infrastructure.DatabaseConfiguration`), reused from every call site instead of re-typing the literal — introduced in [decisions/0005](decisions/0005-auth-identity.md). This is a general convention for future config keys too, not just the database ones; it doesn't apply to the config *files* themselves (`appsettings.json`, `.env`), which necessarily spell the key out as JSON/text.

**CI**: GitHub Actions, two independent workflows each path-filtered to only run when relevant — `.github/workflows/ci-backend.yml` (restore/format/build/test) and `.github/workflows/ci-web.yml` (install/check/test/build) — on every pull request. Advisory only for now, no required status checks. Reasoning: [decisions/0004](decisions/0004-scaffold-backend-and-web-app.md).

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

- Detailed EF Core schema/migrations for the catalog (`Artist`/`Album`/`Track`/`SharePage`) — Identity's own schema is now settled, see [decisions/0005](decisions/0005-auth-identity.md).
- Provider-specific auth and implementation details, per streaming service.
- SPA build glue (Dockerfile or build script) to produce the single deployable artifact.
- Mobile app's secure token storage and refresh-flow UX (the wire mechanism — bearer tokens against the shared `MapIdentityApi` endpoints — is decided, see [decisions/0005](decisions/0005-auth-identity.md)).
- Real transactional email sending for Identity flows (confirmation/reset) — currently a logging no-op sender; tracked as its own roadmap item ("Integrate email service").
- Hosting provider/free-tier specifics not yet evaluated.
- Secrets/config management for provider API keys (depends on the hosting choice).
- Structured logging/observability approach.
- OpenAPI generation for typed clients.
- Dev container/Docker setup for onboarding consistency.
- i18n tooling for the manifesto's localization goal.
