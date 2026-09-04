# 0007. Catalog data model

Status: proposed

## Context

[roadmap.md](../roadmap.md)'s next open item is "Implement the catalog data model + EF Core migrations (Postgres/SQLite)". [decisions/0003](0003-initial-architecture.md) already fixed the rough shape — a provider-agnostic catalog of `Artist`/`Album`/`Track` plus `ProviderLink`s (the reusable "Music Wikipedia" catalog from the manifesto), separate from `SharePage` — but explicitly left "detailed schema and EF Core migrations" as "implementation-time work, not fixed by this ADR." That's what this ADR settles.

This is also the first schema the project designs itself. The only schema-changing feature so far, auth ([decisions/0005](0005-auth-identity.md)), got its tables from the framework (`IdentityDbContext`) — this is the first real exercise of this project's own Guid-key precedent and its dual-provider strategy (Postgres migrations for prod/dev, SQLite `EnsureCreated` for fast tests, a Testcontainers-Postgres integration-test pass for schema-changing features) against a hand-designed schema, not a supplied one.

## Decision

### Entities: `Artist`/`Album`/`Track`, lean, single-artist attribution for v1

All three live in `Hodnota.Domain` as plain classes with no framework dependencies, keyed by `Guid` (the precedent [decisions/0005](0005-auth-identity.md) set for future catalog entities to follow).

- `Artist`: `Id`, `Name` (required).
- `Album`: `Id`, `Title` (required), `ArtistId` (required FK to `Artist`), `ReleaseYear` (nullable `int`). One artist per album.
- `Track`: `Id`, `Title` (required), `ArtistId` (required FK to `Artist`), `AlbumId` (**nullable** FK to `Album`), `TrackNumber` (nullable, meaningful only when `AlbumId` is set). A track doesn't require an album — a standalone single is a `Track` with `AlbumId = null` — and it carries its own `ArtistId` directly rather than inheriting it transitively through a possibly-absent album.

This means `Track.ArtistId` can duplicate `Track.Album.ArtistId` when both are set. That's an accepted denormalization, not an oversight: it keeps "does a track need an album" answered with a plain nullable FK instead of an invariant to enforce, and it sidesteps inventing multi-artist/compilation handling now.

Explicitly deferred as YAGNI, each its own future item if a real need surfaces: genres/tags, cover art/images, track duration/ISRC/other metadata, multi-artist collaborations, and various-artist compilation albums (which would need a future `AlbumArtist(AlbumId, ArtistId, Role)` join table). This is a catalog seeded incidentally by search results, not a full metadata store.

### `ProviderLink`'s association to `Artist`/`Album`/`Track`: nullable FK columns + a CHECK constraint

One `ProviderLink` table: `Id`, `ArtistId`/`AlbumId`/`TrackId` (all nullable FKs), `Provider`, `ExternalId` (required), `ExternalUrl` (required), `Confidence`, `LastVerifiedUtc`. A database CHECK constraint (`HasCheckConstraint`, using the portable `CASE`-based boolean-sum form — not Postgres-only cast syntax — so it runs unchanged on both Npgsql and SQLite) enforces that exactly one of the three FKs is set.

Rejected:
- A shared `CatalogEntry` base/TPT-style table (each of `Artist`/`Album`/`Track` carrying a 1:1 FK to a base table holding just `Id`, letting `ProviderLink` FK to that instead): real mapping ceremony for zero query benefit today — nothing needs "give me all catalog entries polymorphically" yet.
- Three separate per-type join tables (`ArtistProviderLink`/`AlbumProviderLink`/`TrackProviderLink`): triples the entity/config/migration surface and duplicates `Provider`/`Confidence`/`LastVerifiedUtc` three times, for no gain over one table plus a constraint.

### `Provider` persisted as a string, not `int` or a native Postgres enum

`enum StreamingProvider { YouTube, YouTubeMusic, Qobuz, Tidal, Deezer, AppleMusic, Bandcamp }` (matching [architecture.md](../architecture.md)'s supported-services list) lives in `Hodnota.Domain`, mapped on `ProviderLink.Provider` via `.HasConversion<string>()`.

Rejected: native `int` storage — silent-corruption risk if this small, hand-curated list is ever reordered or has a member inserted mid-list. Rejected: a native Postgres enum type — SQLite has no equivalent, which would force divergent column types per provider, breaking the dual-provider portability principle from [decisions/0003](0003-initial-architecture.md). A string is the one representation both providers store identically, and it's directly legible when inspecting the database by hand.

### Catalog `DbSet`s join the existing `ApplicationDbContext`, not a new dedicated `CatalogDbContext`

Catalog `DbSet`s are added to the existing `ApplicationDbContext` (still under `Hodnota.Infrastructure/Identity/` — not moved; a folder rename would be pure churn). This is the project's first `OnModelCreating` override: `base.OnModelCreating(builder)` runs first (required for Identity's own mapping), then `builder.ApplyConfigurationsFromAssembly(...)` picks up new `IEntityTypeConfiguration<T>` classes under `Hodnota.Infrastructure/Catalog/`.

This matches [decisions/0005](0005-auth-identity.md)'s "single Postgres-only migration set" precedent and avoids real bounded-context ceremony — two `__EFMigrationsHistory` tables needing distinct names, two `IDesignTimeDbContextFactory`s — that buys nothing for a solo project with no scaling pressure yet. Revisit only if catalog data ever needs a genuinely separate physical database.

### `Confidence` and `LastVerifiedUtc` are nullable, with no default

`Confidence` (`double?`) and `LastVerifiedUtc` (`DateTimeOffset?`) are both nullable. Matching/verification logic doesn't exist yet — that's future, walking-skeleton-era work — so defaulting either now (e.g. `Confidence = 1.0`, `LastVerifiedUtc = now`) would misrepresent unverified data as verified.

### `SharePage` stays out of scope for this ADR

Deferred to its own ADR when "Implement a first streaming provider end-to-end as a walking skeleton" actually starts. Its shape — ordered/filtered provider-link overrides, its relation to `ApplicationUser` — isn't forced by any concrete requirement yet; designing it now would be speculative, matching this project's repeated scope-discipline precedent ([decisions/0004](0004-scaffold-backend-and-web-app.md), [decisions/0005](0005-auth-identity.md)'s email deferral).

### No repository interfaces yet

This ADR scopes to entities plus `DbContext` configuration and migrations. `Hodnota.Application` stays empty of catalog interfaces (e.g. `IArtistRepository`) until the walking-skeleton item defines a real query/search use case — guessing repository shapes now, against zero callers, risks designing the wrong interface before the actual access pattern (likely intertwined with `IStreamingProvider` search, not plain CRUD) is known.

## Consequences

- Deliberately deferred, each its own future roadmap/ADR item: `SharePage`, multi-artist/compilation albums, genres/images/duration/other metadata, repository interfaces.
- Unlike [decisions/0005](0005-auth-identity.md)/[decisions/0006](0006-openapi-scalar-dev-ui.md), [architecture.md](../architecture.md)'s Data Model and Open Questions sections and [roadmap.md](../roadmap.md)'s catalog checkbox are **not** updated by this ADR itself — they get updated once the schema described here is actually implemented (migrations committed), matching those ADRs' "describes what got built" pattern rather than [decisions/0003](0003-initial-architecture.md)'s meta-architecture pass.
- Establishes the `Hodnota.Infrastructure/Catalog/` `IEntityTypeConfiguration<T>` convention that future non-Identity entities follow.
- Implies new test coverage, not written by this ADR: a SQLite `EnsureCreated`-based fast test alongside `Hodnota.Infrastructure.Tests/Identity/ApplicationDbContextTests.cs`, and a new Postgres integration test file alongside `Hodnota.Infrastructure.IntegrationTests`' `PostgresIdentitySchemaTests.cs`/`PostgresContainerFixture.cs` — including a test that the CHECK constraint actually rejects a zero-or-multiple-FK `ProviderLink` insert against real Postgres.
