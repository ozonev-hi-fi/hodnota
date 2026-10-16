# 0019. Scheduled catalog refresh: Quartz.NET, DB-backed intervals, and the first admin surface

Status: accepted

## Context

[roadmap.md](../roadmap.md)'s "Scheduled catalog refresh" item ([ideas.md](../ideas.md#scheduled-catalog-refresh)) asks for a background job that re-checks existing provider links over time (is the release/track still on the service?) and re-checks "not found" platforms more often (has it appeared since?), reusing the check dates [decisions/0014](0014-resolve-time-catalog-enrichment.md) already saves on `ProviderCheck` (`Outcome`, `CheckedAtUtc`).

ADR 0014 rejected a persistent job library (Hangfire/Quartz.NET) for resolve-time enrichment, explicitly deferring that choice: "The future scheduled refresh ... needs a scheduler; that is the time to revisit this." That time is now. Today, nothing in the repo does periodic/timer-based work — `EnrichmentWorker` is a `BackgroundService` driven by an in-process `Channel<T>` queue (`InProcessEnrichmentQueue`), fired only when a user resolves or watches a share page, not on a schedule. There is also no DB-backed runtime configuration anywhere (all provider config is `appsettings.json`/env-var, read once at startup), and no admin-only surface anywhere in the app — `AddRoles<IdentityRole<Guid>>()` is registered but no role has ever been created or checked.

## Decision

**D1 — Quartz.NET, with `AdoJobStore` when the configured database provider is Postgres, falling back to Quartz's default in-memory `RAMJobStore` otherwise.** Not an in-process `BackgroundService`+`PeriodicTimer` (the `EnrichmentWorker` style), and not Hangfire (its dashboard is an admin-adjacent surface this project hasn't chosen to expose, matching Scalar's dev-only gating). `EnrichmentWorker`'s own queue-driven worker is untouched — an event-driven queue and a periodic sweep are different trigger models that now coexist deliberately.

The Postgres/in-memory split mirrors `DatabaseConfiguration`'s existing provider switch (Postgres gets `dbContext.Database.MigrateAsync()`; SQLite gets `EnsureCreatedAsync()` — SQLite is already the lesser-featured path everywhere else in this codebase, used only by the test suite, never by a real deployment's `appsettings.json`). Quartz's AdoJobStore has no first-class SQLite helper the way it does `UsePostgres(...)`, and the app's test hosts (`CatalogApiFactory`, `AuthApiFactory`, `DependencyInjectionTests`) all boot the full `AddInfrastructure` against SQLite in-memory — giving them a real Postgres-backed job store would mean either a fake connection or new test infrastructure neither is worth it for. The Postgres branch calls `store.ProvisionSchema()` unconditionally (not only in `Development`, as a framework example might suggest) — this project has no separate schema-script deployment step for its one Postgres instance (local dev container or prod, whichever is configured), so auto-provisioning is the only schema path that exists, same spirit as `EnsureCreatedAsync()` for SQLite.

Package additions: `Quartz` and `Quartz.Extensions.Hosting` (both 4.5.1, the current stable line). System.Text.Json serialization is built into Quartz 4.x — no separate serializer package needed.

**D2 — refresh intervals are a DB-backed single-row settings entity (`RefreshSettings`)**, not `appsettings.json`. Defaults: not-found platforms re-checked every 7 days, confirmed links every 90 days. Read through a new `IMemoryCache`-backed cache (same `IMemoryCache` already used by `MemorySearchCandidateCache`, but with explicit invalidation on write — that existing cache is TTL-only, this is the first manual-invalidation use). The admin settings endpoint invalidates the cache after a successful write; the next read repopulates it from the database.

**D3 — YouTube and YouTube Music are out of scope for this feature.** No `search.list` quota counter exists (named as deliberately unbuilt in [decisions/0018](0018-youtube-music-authenticity-search.md)'s addendum); running scheduled refresh against YouTube today would silently compete with resolve-time search for its 100-calls/day budget with no governor. Scope is the other six providers: Discogs, Tidal, Deezer, Apple Music, Spotify, Qobuz. Named explicitly here, the same way ADR 0018 named the Wikidata OLAK lookup as a deliberate, confirmed-with-owner carve-out rather than an oversight. A YouTube-scoped scheduled refresh is a separate future item, gated on the quota counter landing first.

**D4 — refresh candidates are every `ProviderCheck` row for the in-scope platforms, past its interval.** No "recently viewed share page" filter — that activity signal doesn't exist today (the watch stream isn't a persisted view-event log) and building it is out of scope here. Query shape: platform codes limited to `Discogs`/`Tidal`/`Deezer`/`AppleMusic`/`Spotify`/`Qobuz` (excluding `YouTube`/`YouTubeMusic`), rows where `Outcome == NotFound` and `CheckedAtUtc` older than the not-found interval, union rows where `Outcome` is `ExactMatch`/`NameMatch` and `CheckedAtUtc` older than the found interval.

**D5 — the refresh-settings page is the first admin-gated surface in the app, backed by a real `Admin` `IdentityRole<Guid>`.** At startup, if an `Admin:Email` configuration value is set and a user with that email exists, the app ensures the `Admin` role exists and assigns it to that user if not already assigned; if the config value is unset or no matching user exists yet, seeding is a silent no-op (this keeps every existing test host, none of which configure `Admin:Email`, booting unchanged). A new `Admin` authorization policy (`RequireRole("Admin")`) replaces the bare `AddAuthorization()` call; the new settings endpoint requires it. The frontend decodes the `role` claim out of the stored JWT access token (new — no JWT decoding exists anywhere in `web/src` today) to decide whether to show the admin route, via a new `RequireAdmin` guard that wraps `RequireAuth`'s existing pattern rather than editing it.

**Rejected alternatives:**
- In-process `BackgroundService`+`PeriodicTimer` — would have avoided a new dependency entirely, but rejected in favor of Quartz's built-in scheduling and persisted fire-history.
- Hangfire — rejected, its dashboard is unwanted new exposure.
- In-memory Quartz `JobStore` for Postgres deployments too — rejected in favor of `AdoJobStore`'s persisted schedule state, even though the refresh logic itself is idempotent via `CheckedAtUtc` regardless of which store is used.
- Activity-filtered refresh scope (only entities with a recently viewed share page) — rejected, no "last viewed" signal exists yet; building one is undiscussed scope.
- Including YouTube now with no quota governor — rejected as too risky to resolve-time search's own budget.
- A hardcoded owner-user-id/email check instead of a real Identity role — rejected, would leave the already-registered `IdentityRole<Guid>` scaffolding unused and need redoing the day a second admin is needed.

## Consequences

- New NuGet dependencies: `Quartz`, `Quartz.Extensions.Hosting` (4.5.1) — the first scheduler-library dependency in the repo.
- New tables: Quartz's own `QRTZ_*` schema (Postgres only, auto-provisioned), `RefreshSettings` (both providers, via the normal EF migration), and `AspNetRoles`/`AspNetUserRoles` now actually populated for the first time.
- First admin-gated feature in the app. This opens, but does not answer, the broader question of general role-management UI (granting/revoking roles through the product itself) — today, admin status is entirely config-driven at startup.
- YouTube/YouTube Music's own scheduled refresh remains a named open gap; [roadmap.md](../roadmap.md) records it as a follow-up rather than folding it silently into this item's checkbox.
- `CatalogEnrichmentService.EnrichAsync` and `EfCatalogRepository.SaveEnrichmentAsync` are reused unchanged by the new job — the resolve-time enrichment path (`EnrichmentWorker`, `InProcessEnrichmentQueue`) is not modified.
- [roadmap.md](../roadmap.md)'s "Scheduled catalog refresh" line links here and is checked off once implemented.

## Addendum (2026-10-16): D5's frontend half, corrected during implementation

D5 assumed the frontend could decode a `role` claim out of the stored JWT access token. That assumption didn't hold: this app's bearer tokens, issued by `AddIdentityApiEndpoints<ApplicationUser>()`, are opaque Data-Protection-encrypted strings, not JWTs — confirmed by inspecting a real login response (no three-part dot-separated structure, nothing to decode). There is no endpoint that otherwise exposes a user's roles either.

**Corrected design**: the frontend has no local signal for "is this user an admin." Instead, `AuthContext` calls the new `GET /api/admin/refresh-settings` endpoint itself (`checkAdminAccess()` in `web/src/api/adminSettings.ts`) whenever `status` becomes `'authenticated'`, and treats success as admin and any failure (403, 401, network error) as not-admin — there is no finer-grained signal available. `RequireAdmin` reads `isAdmin`/`isAdminLoading` off `AuthContext` instead of decoding anything client-side. No JWT-decoding utility was built, since there is nothing it could ever decode.

**Consequence**: every authenticated page load makes one extra API call (the admin probe) that a real role claim would have avoided. Acceptable for a single-admin, pre-UAT app; worth revisiting (e.g. a small `/api/account/roles` endpoint, or switching the admin policy check client-visible some other way) if this probe pattern needs to generalize to more role-gated features later.

## Addendum (2026-10-16): changes from code review

A review of the implementation found ways the sweep could stop working for good. These changes refine D1, D2, D4 and D5:

- **Every picked candidate moves forward (D4).** The sweep takes the 50 oldest due rows. A row whose `CheckedAtUtc` never changes stays first in line, and 50 such rows block every other row forever. So:
  - Only providers registered right now are queried. Dormant Spotify/Qobuz rows (no credentials) are not selected at all.
  - `Failed` rows are due again after the **not-found** interval. Before this, one provider outage during a sweep turned rows into `Failed`, and the sweep never picked them again.
  - A candidate the sweep picked but could not settle (the page failed, or the save failed) is saved as `Failed`. That moves its `CheckedAtUtc` forward. A `Failed` save only updates the check row; existing links are kept.
- **The trigger is a cron time, top of every hour (D1).** Quartz replaces a stored trigger with the declared one on every boot (`OverwriteExistingData` is on by default). A start time of "now + 1 hour" therefore moved with every restart, so a process restarting more than once an hour never ran the job.
- **Intervals are capped at 3650 days (D2).** Larger values overflow `TimeSpan`/`DateTimeOffset` math and would break every later sweep.
- **The settings cache does not keep a stale value (D2).** A read that overlaps an invalidation is not cached, so a new value always takes effect on the next read.
- **Admin seeding fails startup when Identity reports an error (D5).** It does not continue silently anymore. "No `Admin:Email` set" and "no such user yet" are still silent no-ops.
- **Each share page gets its own DI scope**, as in `EnrichmentWorker`, so one failed save cannot break the rest of the batch. Shutdown cancellation is not logged as an error.

**Checked and kept as is**: test hosts still set `Database:Provider` as an environment variable. Quartz 4.5.1 can defer the store's *connection* (`DataSourceOptions.ConnectionStringName`), but it chooses between the ADO and in-memory store through the DI registrations `AddQuartz` makes, so that choice cannot wait for `Build()`. The variable must also be set before `.env` loads (which uses `NoClobber`) to beat a developer's own `Database__Provider=Postgres`.
