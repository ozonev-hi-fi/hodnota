# Workflow

Day-to-day cheat sheet for the branching/versioning policy decided in [decisions/0001-branching-and-versioning-strategy.md](decisions/0001-branching-and-versioning-strategy.md). See that ADR for the reasoning; this file is just the commands.

## Starting a feature

Branch off `develop`:
```
git checkout develop
git pull
git checkout -b feature/<identifier>-<short-description>
```
`<identifier>` is the relevant ADR number when the task has one (e.g. `feature/0003-initial-architecture`); omit it if there's no ADR (e.g. `feature/fix-readme-typo`). No GitHub Issue is required to start a branch — see [decisions/0001](decisions/0001-branching-and-versioning-strategy.md).

## Finishing a feature

Only once it's fully done: DB/migration changes, business logic, and test coverage all in.

1. Push the branch and open a PR into `develop`.
2. Merge the PR into `develop` (squash or regular merge — no review gate required, this is a solo repo).
3. Delete the feature branch (locally and on `origin`).

## Releasing

Once `develop` is in a release-worthy state:

1. Merge `develop` into `main`.
2. Tag `main` with the next SemVer version (`vX.Y.Z`, pre-1.0 as `v0.x.y` until the first real usable release) and push the tag.

`release/*` and `hotfix/*` branches aren't in use yet — see the ADR for when/how they get introduced.

## Rules of thumb

- Nothing gets committed to `main` directly, other than the one-time docs bootstrap.
- Branch names are always `feature/<identifier>-<short-description>` — `<identifier>` is the relevant ADR number when one exists, otherwise omitted.
- Claude does not run `git commit` / `git push` / PR-creation commands itself in this repo — see `CLAUDE.local.md`. It prints the commands; you run them.
