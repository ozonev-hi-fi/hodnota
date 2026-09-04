# CLAUDE.md

Project-wide guidance for working on this repo. Local, sensitive, or not-for-public-history notes live in the gitignored `CLAUDE.local.md` instead.

## Branch check — every task, no exceptions

Before touching any file, run `git branch --show-current` (or check `git status`) and compare against the "Branching & Versioning Strategy" in [docs/architecture.md](docs/architecture.md) / [docs/workflow.md](docs/workflow.md). Work happens on a `feature/<identifier>-<short-description>` branch cut from `develop` — not directly on `develop` or `main` (the only exception is the one-time docs bootstrap that predates this rule). `<identifier>` is the relevant ADR number when the task has one, otherwise omitted — GitHub Issues are not required to start a branch, see [decisions/0001](docs/decisions/0001-branching-and-versioning-strategy.md).

If the current branch isn't a feature branch: **stop before implementing.** Create/checkout `feature/<identifier>-<short-description>` (or `feature/<short-description>` if there's no ADR) off `develop` and continue there. Re-check the branch again before ever proposing a commit command, in case something moved it in the meantime.

## Workflow: decision doc before implementation

For a task big enough to warrant its own [ADR](docs/decisions/README.md) — non-obvious, debated, or otherwise likely to be silently re-litigated by someone (including future us) who doesn't know why it was made — work proceeds in this order:

1. **Branch check** — see above.
2. **Decision doc** — write (or update) `docs/decisions/NNNN-short-title.md` first, describing the problem, the options considered, and the decision. This is the "ticket": what's being solved and why, settled before any implementation planning starts. Use the format in [docs/decisions/README.md](docs/decisions/README.md).
3. **Plan** — design the implementation approach against that decision doc (plan mode), and get it approved before writing code.
4. **Implement**.
5. **Review** — review the changes before anything is committed.
6. **Commit** — developer (human) only action. Re-check the branch (see above) and suggest the best commit/push/PR commands for the user to run.

Small/routine tasks skip the decision doc and go straight to a branch, per [docs/roadmap.md](docs/roadmap.md) — this rule doesn't change that split, it only formalizes the order for the tasks that do cross the ADR bar. The branch check above applies regardless of which path a task takes.

## Code comments: don't duplicate the ADR

Every feature big enough to need a decision doc ships with one, permanently linkable from the PR. That changes the bar for an inline comment: don't restate a design decision, its rationale, or its rejected alternatives in code when the ADR already covers it — link to the ADR from the PR/commit, not from a comment, and trust a reader to open it. This is on top of the general "why, not what" comment rule: no comment that's obvious from the code, the tests, or the relevant ADR.

Property/column meaning that isn't obvious from its name (what a field represents, not why it's shaped that way) is documentation, not a design-decision comment — prefer a `[Description("...")]` attribute on the member over a `//` comment for that, so it stays attached to the type for any future self-documentation/reflection use, not just readers of the source file.
