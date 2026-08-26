# 0001. Trimmed GitFlow with ADR-numbered / optional issue-numbered feature branches

Status: accepted

## Context

This is a solo pet project with no shipped app yet — just an initial commit and a set of process docs. The goal is to work each task in isolation (own branch, fully implemented and tested — DB changes, business logic, test coverage) before integrating it, without adopting branch types (`release/*`, `hotfix/*`) that have no real use case yet.

## Decision

- `main` — always deployable. Advances only via merge plus a SemVer tag (`vX.Y.Z`), starting pre-1.0 (`v0.x.y`). No direct commits going forward, other than the one-time docs bootstrap that preceded this ADR.
- `develop` — integration branch and default base for new work. Represents the current "alpha" state: everything merged toward the next release, not yet hardened or tagged.
- `feature/<identifier>-<short-description>` — one branch per task, branched off `develop`, merged back into `develop` once fully implemented and tested. `<identifier>` is the relevant ADR number when the task has one (e.g. `feature/0003-initial-architecture`), otherwise omitted (e.g. `feature/fix-readme-typo`). No GitHub Issue is required to start or name a branch — for a solo project, filing one before every branch was overhead without payoff. GitHub Issues remain available later as an optional tool for actual bug/backlog tracking, just not as a branching gate.
- `release/*` and `hotfix/*` are deferred, not adopted now. Add `release/*` (cut from `develop`, stabilization only, merges to `main` and back to `develop`) when preparing an actual release; add `hotfix/*` (cut from `main`, merges to `main` and `develop`) when a real production fix is needed.

See [../workflow.md](../workflow.md) for the day-to-day commands.

## Consequences

- Every feature goes through a PR into `develop`, even solo — slight overhead, but keeps history clean.
- Branch names don't auto-link to a GitHub Issue; if issue tracking is adopted later, that link would be manual.
- `release/*`/`hotfix/*` will need this ADR revisited (or a new one) once they're actually introduced.
