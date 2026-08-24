# Architecture

This is the living design document for the project. Unlike [MANIFESTO.md](MANIFESTO.md), which is frozen, this file should be kept in sync with reality as decisions are made or revised. When a choice is worth explaining *why*, capture it as an entry under [decisions/](decisions/) and link it from here instead of re-arguing it in place.

Status: pre-implementation — this reflects the initial thinking from the manifesto and has not yet been through a real design pass.

## Components

- **API** — .NET stack. Owns the databases, business logic, and integration with third-party streaming services.
- **Web UI** — React.
- **Mobile app** — .NET MAUI. Partially mirrors the Web UI, but its core purpose is share-sheet integration: a user shares an album/artist/song from a streaming app, picks this app from the share sheet, it finds or creates the corresponding sharing page, and from there the user shares a link to *that* page onward (to a messenger, etc.).
- **Satellite services** (later) — additional services built on top of the same core, e.g. a Telegram bot that watches for streaming links in a chat and replies with a page collecting the equivalent links across other services. A third-party-facing API may be needed to support these.

## Authentication & Authorization

Email + password, Google, Facebook. Possibly Apple Sign-In later.

## Hosting

Needs to be free (or effectively free) to start. Candidates to evaluate: containerization, Azure's free tier(s). Must also consider scalability if the product gets traction, and baseline security posture (attack surface, data leak prevention, a kill switch, disaster-recovery scenarios kept up to date).

## Data Model (rough)

A persisted catalog of artists, albums, and songs, built up as a side effect of the searches needed to construct sharing pages — intended to be reusable by future, unrelated projects (a "Music Wikipedia").

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
- **`feature/<issue#>-<short-description>`** — one branch per GitHub Issue, branched off `develop`. Merged back into `develop` only once the feature is fully done: DB changes, business logic, and test coverage all in.
- **`release/*`, `hotfix/*`** — deferred, not created yet. To be added when actually needed:
  - `release/*` — cut from `develop` when preparing an actual release; stabilization only; merges to both `main` (tag) and back to `develop`.
  - `hotfix/*` — cut from `main` for urgent production fixes; merges to both `main` (patch tag) and `develop`.

## Open Questions

- Detailed API/domain design has not been done yet (see roadmap).
- Hosting provider/free-tier specifics not yet evaluated.
