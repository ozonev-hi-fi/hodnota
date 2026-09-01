# 0005. Auth: ASP.NET Core Identity (email + password)

Status: accepted

## Context

[roadmap.md](../roadmap.md)'s next open item is "Implement auth (Identity)" — email + password only; Google/Facebook external login is a separate, later roadmap item, scheduled after hosting is settled. [decisions/0003](0003-initial-architecture.md) already fixed the high-level shape (ASP.NET Core Identity, PostgreSQL-backed) but explicitly left the cookie-vs-JWT token strategy open, "to settle in its own ADR when auth is actually implemented" — that's this one.

This is also the first EF Core `DbContext` and the first schema-changing feature in the project, so it's the first time the "schema changes need a real-PostgreSQL integration-test pass" rule ([decisions/0003](0003-initial-architecture.md) / [architecture.md](../architecture.md)'s Database section) actually applies — and the first time the project needs a real local database, which reopens the "SQLite for local dev and fast tests" line in that same section.

## Decision

### Token strategy: bearer tokens via `MapIdentityApi`, not cookies

ASP.NET Core Identity's built-in `AddIdentityApiEndpoints<TUser>()` / `MapIdentityApi<TUser>()` (introduced in .NET 8, still current) is used in its default bearer-token mode, not the cookie mode the same endpoints also support (`useCookies=true`).

The deciding factor is the future MAUI mobile client: it needs to authenticate with no browser cookie jar, and the standard MAUI pattern is a bearer token held in platform secure storage. Using one mechanism for every client — the SPA now, mobile later, any third-party-facing API later ([architecture.md](../architecture.md)'s "Satellite services") — avoids running two parallel auth code paths (cookie scheme + bearer scheme) in the API. It also means almost no custom endpoint code: `/register`, `/login`, `/refresh`, `/forgotPassword`, `/resetPassword`, `/manage/*` ship out of the box.

This is a real trade-off, noted explicitly: cookies (httpOnly) would be somewhat more XSS-resistant for the SPA specifically, but that benefit doesn't extend to mobile, so it doesn't change the uniform-mechanism conclusion. Accepted for v1; revisit in a later hardening pass if needed.

Interaction with deferred items:
- **External login (Google/Facebook)**: unaffected — those handshakes end in the same `UserManager`/token issuance, no rework implied.
- **Mobile (MAUI)**: this decision *is* the mobile auth wire mechanism. What's still open is on-device secure token storage and the refresh-flow UX, not the mechanism itself.
- **Web SPA login UI**: out of scope for this task (see below) — when it lands, it calls the same bearer endpoints and holds the access token in memory (not `localStorage`), with the refresh token handling renewal. CORS config for the SPA dev server is deferred to that step.

### `ApplicationUser` lives in `Hodnota.Infrastructure`, not `Hodnota.Domain`

`IdentityUser<TKey>` is a framework base class (`Microsoft.AspNetCore.Identity`) that already carries the auth mechanics — `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `LockoutEnd`/`LockoutEnabled`, `AccessFailedCount`, plus `Id`/`UserName`/`Email` — and `IdentityDbContext<TUser, TRole, TKey>` already knows how to map it (and roles/claims/logins/tokens) to tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetUserRoles`, `AspNetRoleClaims`) via its own `OnModelCreating`. `ApplicationUser : IdentityUser<Guid>` is the concrete type EF Core needs to map — a thin subclass, not a hand-designed entity.

It's deliberately **not** a Domain entity: password hashes and lockout state are infrastructure/auth-mechanism concerns, not business rules, and `Hodnota.Domain` has a hard no-framework-dependencies rule (per [decisions/0003](0003-initial-architecture.md)'s solution layout). If the app later needs real domain behavior about users (a profile, preferences), that becomes a separate Domain entity — e.g. `UserProfile`, keyed by the same `Guid` id — decoupled from the Identity mechanism. Not built now: no profile fields exist yet, matching [decisions/0004](0004-scaffold-backend-and-web-app.md)'s scope-discipline precedent.

`IdentityRole<Guid>` is wired up now too (`AddRoles<IdentityRole<Guid>>()`), even with no role-based authorization logic yet — schema-only cost, avoids a second migration later purely for roles.

`Guid` was picked as the key type (`IdentityUser<Guid>`/`IdentityRole<Guid>`) as the idiomatic default for a PostgreSQL-backed system — there's no existing catalog-entity precedent to match yet, so this sets one for `Artist`/`Album`/`Track`/`SharePage` to follow, and `SharePage.OwnerId` will be a `Guid` FK to this table regardless.

### Local interactive dev uses a local PostgreSQL container, not SQLite

A `docker-compose.yml` at repo root runs `postgres` with a named volume, so local data persists across restarts. `appsettings.Development.json` sets `Database:Provider` to `Postgres`; the connection string itself comes from the root `.env` file (see below), and the same versioned EF Core migrations used in production are applied to it (`Database.Migrate()`), giving a **single Postgres-only migration set** — no per-provider migration duality, which EF Core doesn't support cleanly for one `DbContext` anyway (colliding model snapshots without extra ceremony).

SQLite is scoped down to just the fast, Docker-free automated test projects (`Hodnota.Api.Tests`, `Hodnota.Infrastructure.Tests`), which configure an in-memory SQLite connection directly in their test host setup — independent of `appsettings.Development.json` — since those are throwaway per-run databases, using `Database.EnsureCreated()` rather than migrations (nothing to keep in sync when the database is regenerated from the live model every run).

This refines [architecture.md](../architecture.md)'s Database section ("SQLite for local dev and fast tests" → "SQLite for fast automated tests only; local interactive dev uses a local Postgres container"). It's a project-wide refinement surfaced by this task — not an auth-only rule — since it also applies to the next roadmap item (catalog data model).

The real-PostgreSQL integration-test pass required for schema-changing features (per [decisions/0003](0003-initial-architecture.md)) is satisfied separately, via Testcontainers in a new `Hodnota.Infrastructure.IntegrationTests` project — a disposable, ephemeral instance, deliberately not the same one as the persistent local dev container.

### Email confirmation disabled for v1

`RequireConfirmedAccount = false`; a `NoOpEmailSender : IEmailSender<ApplicationUser>` logs confirmation/reset links via `ILogger` instead of sending real email (register/forgot-password fail without *some* `IEmailSender` registered). Real transactional email is explicitly not built now — the roadmap gets two new items instead of building it "while we're in there": **Integrate email service**, then **Use email service for auth confirmation flows**, placed after "Figure out hosting" (email credentials need the same hosting-dependent secrets story as provider API keys) and before "Implement auth (Google/Facebook external login)". The `IEmailSender` abstraction here is the placeholder seam that later step swaps a real implementation into — no auth code needs to move.

### Backend only

No `/web` changes — no login/register UI, no CORS/proxy wiring. Matches [decisions/0004](0004-scaffold-backend-and-web-app.md)'s scope discipline. Web (and later mobile) auth UI become their own explicit items in [roadmap.md](../roadmap.md) rather than being implicitly bundled into "walking skeleton."

### Local dev credentials: `.env` (committed) + `.env.local` (gitignored override), not duplicated literals

The local dev Postgres credentials (`hodnota`/`hodnota`, non-secret — localhost-only, never shipped anywhere) were initially hardcoded in both `docker-compose.yml` and `appsettings.Development.json`. Instead, the standard two-tier `.env`/`.env.local` convention (Vite, Next.js, Rails, etc.) is the source of truth: root `.env` holds the shared defaults and is deliberately committed, since none of it is a real secret — `docker-compose.yml` reads it via Compose's implicit `.env` support, and `Hodnota.Infrastructure.DotEnvLoader` (built on the `DotNetEnv` package) loads it into process environment variables for both `Hodnota.Api` (in `Program.cs`, before `WebApplication.CreateBuilder(args)` runs) and `dotnet ef` design-time tooling (`ApplicationDbContextFactory`), searching upward so it resolves regardless of the caller's working directory. `ConnectionStrings:Default` is set exactly once, as `ConnectionStrings__Default` in `.env`, picked up by .NET's standard environment-variable configuration binding. An optional `.env.local` — always gitignored, per the standard convention, for a personal override or a future real secret — loads after `.env` and wins on conflicts; nothing needs one today, but the mechanism is in place. `.env` being committed is a deliberate exception to "never commit `.env`," not a slip — see the file's own place in `README.md` and `.gitignore`.

### Config key/value literals go in a constants class

Repeated config strings (`"Database:Provider"`, `"Postgres"`, `"Sqlite"`, the `"Default"` connection-string name) are pulled into `Hodnota.Infrastructure.DatabaseConfiguration` (a `public static class` of `const string` fields) and reused from every call site (`DependencyInjection`, `Program.cs`, `ApplicationDbContextFactory`, `AuthApiFactory` in tests) instead of re-typing them. This is adopted as a general convention going forward for future config keys/values too, not just these — see [architecture.md](../architecture.md)'s Tooling & Conventions section. It doesn't apply to config *files* (`appsettings.json`, `.env`), which unavoidably spell the key out as JSON/text.

### EF Core migrations are exempted from IDE style analysis

`dotnet ef migrations add`-generated files (`Migrations/*.cs`) aren't hand-edited — regenerating a migration would just reintroduce whatever style its templates emit (e.g. a block-bodied single-statement lambda where this codebase's `.editorconfig` otherwise prefers an expression body). Rather than fight the generator, `.editorconfig` marks `src/**/Migrations/*.cs` as `generated_code = true`, which suppresses IDE/analyzer style suggestions (not real compile errors) for that glob — recognized by both `dotnet format` and Visual Studio's own Roslyn analyzer engine.

### Migrations & endpoints — mechanics

- `ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` lives in `Hodnota.Infrastructure/Identity/`, per [architecture.md](../architecture.md)'s component breakdown ("Infrastructure implements ... EF Core DbContext/migrations, provider clients, auth integrations").
- An `IDesignTimeDbContextFactory<ApplicationDbContext>` is needed because the `DbContext` lives in a class library with no runnable entry point of its own.
- `MapIdentityApi<ApplicationUser>()` is used as-is, unwrapped, mounted under `/api/auth` — the prefix keeps a clean namespace for future non-Identity routes and avoids colliding with SPA client-side routes once the SPA is served same-origin. The unused `/manage/*` surface (2FA, personal data) ships idle; `MapIdentityApi` doesn't offer cheap per-endpoint exclusion and leaving it reachable costs nothing for now.

## Consequences

- Deliberately deferred: external login providers, web/mobile auth UI, real transactional email, `ApplicationUser` profile fields, on-device token storage/refresh UX for mobile — each now has its own roadmap item instead of being silently bundled into this one or "walking skeleton."
- [architecture.md](../architecture.md) is updated: the Authentication & Authorization section describes what got built; the Database section reflects the SQLite-for-fast-tests-only refinement; "Open Questions" drops the now-resolved cookie-vs-JWT item and narrows the mobile-auth item to token storage/refresh UX.
- [roadmap.md](../roadmap.md)'s auth item is checked off, and new items for the deferred work listed above are inserted there.
- This is the first schema-changing feature in the project, so it's also the first real exercise of the Testcontainers-PostgreSQL integration-test rule from [decisions/0003](0003-initial-architecture.md) — `Hodnota.Infrastructure.IntegrationTests` is created as a new project for it, not folded into the existing fast `Hodnota.Infrastructure.Tests`.
- Running the API locally now requires Docker (for the local Postgres container) — a step up from the previous zero-dependency SQLite-file default, accepted in exchange for a stable, persistent local dataset and full schema parity with production.
