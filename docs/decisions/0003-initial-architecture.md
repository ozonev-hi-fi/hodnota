# 0003. Initial architecture for v1

Status: accepted

## Context

[roadmap.md](../roadmap.md) called for "design the detailed architecture of the first version" as its next open item. Until now, [architecture.md](../architecture.md) only carried the manifesto's rough shape — components named, nothing about how they're actually built. This ADR is the first real design pass: a .NET API in Clean Architecture, an abstraction for talking to the different streaming services, a database strategy, how the React web UI is hosted, and a sketch (not a build-out) of how the future MAUI mobile app fits in. Satellite services (e.g. a Telegram bot) stay out of scope for this pass.

## Decision

### Solution layout

Clean Architecture, one dependency direction: Api → Infrastructure → Application → Domain. Infrastructure implements Application's interfaces; Application and Domain never depend outward.

```
/hodnota.slnx
/Directory.Packages.props   # central package management, one file for every .NET project below
/.editorconfig
/src
  /Hodnota.Domain          # entities, value objects — no dependencies
  /Hodnota.Application     # use cases, interfaces (IStreamingProvider, repositories), DTOs
  /Hodnota.Infrastructure  # EF Core DbContext/migrations, provider clients, auth integrations
  /Hodnota.Api             # ASP.NET Core host: endpoints, DI composition, serves the SPA
  /Hodnota.Contracts       # DTOs shared with the future MAUI app
  /Hodnota.Mobile          # not scaffolded yet — placeholder for the future MAUI app
/tests
  /Hodnota.Domain.Tests
  /Hodnota.Application.Tests
  /Hodnota.Infrastructure.Tests
  /Hodnota.Api.Tests
/web                       # React app (Vite + TypeScript), own package.json/toolchain, independent of the API during dev
```

`Hodnota.Mobile` sits under `/src` with the other .NET projects — it's a `.csproj` in the same `.slnx`/MSBuild/CPM graph as everything else. `/web` is the one genuinely separate toolchain (npm/Vite), so it stays outside `/src`.

### Streaming-provider abstraction

- `IStreamingProvider` lives in `Hodnota.Application`: roughly `SearchAsync(query)`, `TryMatchAsync(canonical track/album)`, `ResolveUrlAsync(url)`. Each provider decides how to satisfy these.
- One implementation class per service in `Hodnota.Infrastructure.Providers.<Service>` (YouTube/YouTube Music, Qobuz, Tidal, Deezer, Apple Music, Bandcamp), each owning its own auth (API key/OAuth/JWT, whatever that service requires), a named `HttpClient` via `IHttpClientFactory`, and its own rate-limit/retry policy (Polly).
- Registered via DI as `IEnumerable<IStreamingProvider>`; an Application-layer aggregator fans a search out to all registered providers and merges results into the catalog.
- Provider-specific auth details and per-service quirks are issue-level work, not decided here — the interface is what's fixed by this ADR, not any given provider's implementation.

### Data model (rough)

- Catalog (shared, provider-agnostic): `Artist`, `Album`, `Track`, plus `ProviderLink` (entity + provider + external ID/URL + confidence + last-verified) — the reusable "Music Wikipedia" catalog from the manifesto.
- `SharePage`: created by a user, references a canonical catalog entity (or an unresolved ad-hoc entry), carries its own ordered/filtered list of provider links to display — a page owner can exclude a link even if the catalog has it.
- `User`/identity via ASP.NET Core Identity.
- Detailed schema and EF Core migrations are implementation-time work, not fixed by this ADR.

### Database

EF Core over PostgreSQL in production, SQLite for local dev and fast tests.

SQLite and PostgreSQL diverge on some EF Core translations and types (case sensitivity, `jsonb`, etc.), so any schema-changing feature needs an integration-test pass against real PostgreSQL (e.g. Testcontainers) before merging — SQLite alone isn't sufficient sign-off. This becomes a testing/roadmap follow-up, not something this ADR solves.

### Web UI hosting

`/web` — React + Vite + TypeScript.

- Dev: independent Vite dev server, proxying API calls to the .NET API (CORS or Vite proxy config) — two processes, fast HMR.
- Prod: `npm run build` output copied into the API's `wwwroot`; `Hodnota.Api` serves it via `UseSpaStaticFiles`/`MapFallbackToFile`, so the whole app ships as one deployable unit. The build-glue (Dockerfile or build script) is a hosting-item follow-up, only referenced here.

### Mobile app (MAUI) — sketch only

- Talks to the same `Hodnota.Api`; no separate backend.
- `Hodnota.Contracts` is shared between Api and the future Maui project so request/response DTOs aren't duplicated (C#-to-C#, unlike the TypeScript web client).
- Core purpose stays share-sheet integration per the manifesto: OS share target → app resolves/creates a `SharePage` via the API → user shares the resulting hodnota link onward.
- Auth approach for a native client (token-based vs. delegating to native OAuth SDKs) is an explicit open question, deferred to when mobile work actually starts.

### Auth

ASP.NET Core Identity (PostgreSQL-backed) + external login providers (Google, Facebook now; Apple later, per the manifesto). Token strategy — cookie for the same-origin SPA vs. JWT for the future mobile/third-party clients — is flagged as an open question to settle in its own ADR when auth is actually implemented, not decided here.

### Tooling & conventions

- **Versions**: track the latest *stable* release of everything — .NET SDK, C# language version, React, TypeScript, Node LTS, and NuGet/npm package versions — rather than pinning to whatever was current when a project was scaffolded. Upgrade promptly after each stable release; only pin back deliberately, for a stated compatibility reason.
- **Solution file**: `.slnx` (the newer XML-based solution format) instead of the classic `.sln` — plain-text-diffable, no GUID soup. Verify current SDK/IDE support for it at scaffold time rather than assuming; falling back to `.sln` is a trivial, reversible choice if needed.
- **Central Package Management**: a single `Directory.Packages.props` at repo root (`ManagePackageVersionsCentrally=true`) covering every .NET project, including `Hodnota.Mobile` — one source of truth per package version, no need to split by project since MAUI-only packages simply go unused elsewhere in the same file.
- **Code style**: a root `.editorconfig` covering both C# (backed by the built-in .NET analyzers, `AnalysisLevel=latest`) and the web app. `dotnet format --verify-no-changes` as the enforcement point.
- **Web linting/formatting**: Biome — a single tool for linting and formatting JS/TS/TSX, replacing the ESLint+Prettier combo, with native TypeScript support (no `@typescript-eslint` plugin needed).
- **Testing**:
  - Unit: xUnit + AwesomeAssertions (the actively-maintained, non-commercial fork of FluentAssertions) + NSubstitute for mocking.
  - Integration: `WebApplicationFactory` for API-level tests; Testcontainers running real PostgreSQL for schema-touching features (per the Database section above).
  - E2E: Playwright, tool decided now so project conventions exist, but actual E2E specs wait until there's a real vertical slice (UI + API + DB) worth clicking through — writing them earlier has nothing to exercise.

## Consequences

- Deliberately deferred to follow-up roadmap items, not solved by this ADR: solution/test scaffolding, provider-specific auth, detailed EF Core schema, SPA build glue, and the mobile/auth token strategy.
- Also explicitly out of scope for this ADR, to be decided when they actually come up: CI pipeline (build/test/lint on every PR), secrets/config management for provider API keys (depends on the hosting choice), structured logging/observability, OpenAPI generation for typed clients, a dev container/Docker setup for onboarding, and the i18n tooling for the manifesto's localization goal.
- [architecture.md](../architecture.md) is updated to reflect this design, linking back here for the "why."
- [roadmap.md](../roadmap.md)'s architecture item is marked done and its "break into steps" item is replaced with the concrete epics this ADR implies.
