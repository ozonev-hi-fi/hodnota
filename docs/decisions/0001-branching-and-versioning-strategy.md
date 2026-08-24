# 0001. Trimmed GitFlow with issue-numbered feature branches

Status: accepted

## Context

This is a solo pet project with no shipped app yet — just an initial commit and a set of process docs. The goal is to work each task in isolation (own branch, fully implemented and tested — DB changes, business logic, test coverage) before integrating it, without adopting branch types (`release/*`, `hotfix/*`) that have no real use case yet.

## Decision

- `main` — always deployable. Advances only via merge plus a SemVer tag (`vX.Y.Z`), starting pre-1.0 (`v0.x.y`). No direct commits going forward, other than the one-time docs bootstrap that preceded this ADR.
- `develop` — integration branch and default base for new work. Represents the current "alpha" state: everything merged toward the next release, not yet hardened or tagged.
- `feature/<issue#>-<short-description>` — one branch per GitHub Issue, branched off `develop`, merged back into `develop` once fully implemented and tested. `<issue#>` is the GitHub Issue number, not a hand-maintained counter, so branches, issues, and PRs auto-link.
- `release/*` and `hotfix/*` are deferred, not adopted now. Add `release/*` (cut from `develop`, stabilization only, merges to `main` and back to `develop`) when preparing an actual release; add `hotfix/*` (cut from `main`, merges to `main` and `develop`) when a real production fix is needed.

See [../workflow.md](../workflow.md) for the day-to-day commands.

## Consequences

- Every feature goes through a PR into `develop`, even solo — slight overhead, but keeps history and the branch↔issue link clean.
- Task-level work must be filed as GitHub Issues before a feature branch can be named (already called for in [../roadmap.md](../roadmap.md)).
- `release/*`/`hotfix/*` will need this ADR revisited (or a new one) once they're actually introduced.
