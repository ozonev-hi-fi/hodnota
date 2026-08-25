# CLAUDE.md

Project-wide guidance for working on this repo. Local, sensitive, or not-for-public-history notes live in the gitignored `CLAUDE.local.md` instead.

## Branch check — every task, no exceptions

Before touching any file, run `git branch --show-current` (or check `git status`) and compare against the "Branching & Versioning Strategy" in [docs/architecture.md](docs/architecture.md) / [docs/workflow.md](docs/workflow.md). Work happens on a `feature/<issue#>-<short-description>` branch cut from `develop` — not directly on `develop` or `main` (the only exception is the one-time docs bootstrap that predates this rule).

If the current branch isn't a feature branch: **stop before implementing.** File or pick a GitHub Issue, get its number from the user (issue creation is a shared/visible action — print the `gh issue create` command rather than running it), then create/checkout `feature/<issue#>-<short-description>` off `develop` and continue there. Re-check the branch again before ever proposing a commit command, in case something moved it in the meantime.

## Workflow: decision doc before implementation

For a task big enough to warrant its own [ADR](docs/decisions/README.md) — non-obvious, debated, or otherwise likely to be silently re-litigated by someone (including future us) who doesn't know why it was made — work proceeds in this order:

1. **Branch check** — see above.
2. **Decision doc** — write (or update) `docs/decisions/NNNN-short-title.md` first, describing the problem, the options considered, and the decision. This is the "ticket": what's being solved and why, settled before any implementation planning starts. Use the format in [docs/decisions/README.md](docs/decisions/README.md).
3. **Plan** — design the implementation approach against that decision doc (plan mode), and get it approved before writing code.
4. **Implement**.
5. **Review** — review the changes before anything is committed.
6. **Commit** — developer (human) only action. Re-check the branch (see above) and suggest the best commit/push/PR commands for the user to run.

Small/routine tasks skip the decision doc and go straight to a GitHub Issue, per [docs/roadmap.md](docs/roadmap.md) — this rule doesn't change that split, it only formalizes the order for the tasks that do cross the ADR bar. The branch check above applies regardless of which path a task takes.
