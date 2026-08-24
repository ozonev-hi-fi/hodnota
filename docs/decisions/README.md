# Architecture Decision Records

Short records of decisions worth remembering the *why* of — the equivalent of a Confluence decision page, kept as plain text next to the code instead.

Not every choice needs one. Write an ADR when a decision was non-obvious, was debated, or would otherwise get silently re-litigated or reversed by someone (including future you) who doesn't know why it was made. Skip it for anything easily re-derived from the code itself.

## Format

One file per decision: `NNNN-short-title.md`, numbered sequentially, never renumbered or reused even if a decision is later superseded.

```markdown
# NNNN. Title

Status: proposed | accepted | superseded by NNNN

## Context
What problem or question forced this decision.

## Decision
What was decided.

## Consequences
What this makes easier or harder, and any trade-offs accepted.
```

A superseded record stays in place with its status updated — it's still useful history, just no longer current. Update [architecture.md](../architecture.md) to reflect the current decision, and link back to the ADR for the reasoning.

Not every decision belongs here. A few sensitive ones live in the gitignored `CLAUDE.local.md` at the repo root instead, deliberately kept out of public repo history — check there too.
