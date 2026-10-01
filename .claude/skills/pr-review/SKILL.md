---
name: pr-review
description: Reviews a hodnota GitHub pull request with the `gh` CLI and reports findings plus a fix plan in chat — no code changes, nothing posted to GitHub. Use when asked to review a pull request, check PR N, or `/pr-review N`.
---

A **chat-only finding** is one whose wording must never reach a public PR comment, even summarized — read `CLAUDE.local.md` first (Step 4) to know which ones those are this session. Everything else in this skill is a normal finding.

This skill never edits code and never posts to GitHub by itself (Step 6). The deliverable is the chat report in Step 5.

## Step 0: Preflight

- `git branch --show-current` and `git status` — read-only, just to know where you are; reviewing a PR needs no branch change.
- `gh auth status` — read-only `gh pr`/`gh api` calls work under either stored account on a public repo. Pass `--repo ozonev-hi-fi/hodnota` on every `gh` call regardless, so the target is never ambiguous.

## Step 1: Pull the PR facts

```bash
gh pr view <N> --repo ozonev-hi-fi/hodnota --json number,title,state,isDraft,author,baseRefName,headRefName,baseRefOid,headRefOid,additions,deletions,changedFiles,body,url,reviewDecision,statusCheckRollup,commits,comments,reviews
```

Note the empty/non-empty `body` (a missing PR description plus an ADR-worthy change is a finding on its own), the commit list, and whether CI (`statusCheckRollup`) is green — green CI is not a substitute for reading the code, only a reason not to re-derive what CI already checks (build, tests, lint).

## Step 2: Get the full diff

Compare `git rev-parse HEAD` and `origin/<baseRefName>` against `headRefOid`/`baseRefOid` from Step 1:

- **They match** (you already have the PR branch checked out, base fetched): use `git diff <baseRefName>...HEAD` — three dots, so the comparison starts at the merge base, not at `origin/<base>`'s current tip.
- **They don't**: `gh pr diff <N> --repo ozonev-hi-fi/hodnota --name-only` for the file list, then `gh pr diff <N> --repo ozonev-hi-fi/hodnota` for the full diff, or fetch and check out the PR branch first.

Start with `--stat` (or the `--name-only` list) to size the change before reading it.

## Step 3: Read in layers

A diff over roughly 30 KB of tool output gets truncated to a saved-file pointer instead of shown inline — expect this on anything past a handful of files, and Read the saved file rather than re-running the same diff. Pull one layer's diff at a time, in this order, so each layer's context is still fresh when you read the next:

1. The ADR, if the branch name carries a number (`feature/NNNN-...`) — read it in full before any code; it is the spec the code is reviewed against.
2. Other changed docs (`docs/*.md`, skills).
3. `Hodnota.Domain` → `Hodnota.Application` → `Hodnota.Contracts` — the shapes everything else depends on.
4. `Hodnota.Infrastructure` — repositories, background workers, providers, migrations.
5. `Hodnota.Api`.
6. `web/`.
7. Tests, last — they tell you what the author believed was worth checking, which is a finding in itself when something isn't covered.

A file that's new or mostly rewritten is cheaper to `cat` in full than to read as a diff.

## Step 4: The hodnota checklist

Apply every row below to what Step 3 surfaced; a row with nothing to say is still worth a line in the "Checked, no problem found" part of the report (Step 5), not silence.

| Check | What it catches |
|---|---|
| Code vs ADR | A concrete ADR claim ("uses the built-in X", "asked once", a fixed list of states) that the code doesn't actually do. |
| ADR status | `Status: proposed` left in place at merge time — every accepted ADR in this repo reads `accepted`. |
| PR description | Empty or thin body on a change that has (or needs) an ADR — the ADR is linked from the PR, not restated in a code comment ([CLAUDE.md](../../../CLAUDE.md)). |
| Comments restating the ADR | Delete, don't shorten to a bare doc-link — trust the reader to open the ADR. A field's own meaning (not a design decision) belongs in a `[Description]` attribute instead. |
| **Chat-only material** | Open `CLAUDE.local.md` and hold its topics in mind while reading the diff — a docs/comment change that states one of its decisions' sensitive reasoning in public, permanent history is a finding, but its *write-up* in your report must not repeat the sensitive wording either; describe the problem and point at the file/line instead. |
| Excluded services | A new platform/provider that the project's own exclusion rule (`CLAUDE.local.md`) rules out. |
| Background work's failure paths | A queue/worker/background job: does every path (success, save failure, job failure, shutdown) tell whoever is waiting that it's done? A silent failure path shows up only as a stuck spinner or a timeout, never as an exception anyone sees. |
| Anonymous endpoint cost | What can a logged-out caller trigger, and against what quota (a vendor's daily call limit, a per-minute rate limit) — anonymous is fine, unbounded anonymous cost on a scarce quota is a finding. |
| One rule, two code paths | Two places that render or decide the same thing (e.g. a synchronous read path and a live-update path) must apply the same filter/rule — check them side by side, not just each in isolation. |
| Migration vs code | Every index/constraint name the code checks by string (unique-violation handling, etc.) actually exists in the migration with that name; Designer + snapshot files present. |
| Contracts | A response field left over from before the change, now unused or duplicating a new one; the generated client (`web/src/api/generated/openapi-types.ts`) regenerated to match. |
| Test coverage | Every new class with real logic has a test file; a "someone could forget to update list X" risk is better served by a test that enforces X than a comment asking nicely. |

## Step 5: Report

Chat only — severity groups (High / Medium / Low), each finding as `file:line`, the concrete before/after or example, **why it matters** (the principle, not just the fix), and a fix plan the next session can execute without re-deriving your reasoning. Close with "Checked, no problem found" for the rows that came up clean, then any open questions only the repo owner can settle (a tradeoff, a scope call). Write it in plain, short-sentence English — this user is B2-level and wants correct terminology with a short gloss the first time a term appears, not simplified technical content.

Never apply the findings as edits in this step; implementation is a separate, later task.

## Step 6: If the user wants it on GitHub

Still don't post it yourself. Strip every chat-only finding, save the rest to a file (e.g. `C:\tmp\pr-<N>-review.md`), and print the exact commands for the user to run — this repo's rule is that Claude never executes `git commit`/`push`/PR-publishing commands, posting a PR review included:

```bash
gh auth switch --hostname github.com --user ozonev-hi-fi
gh pr review <N> --repo ozonev-hi-fi/hodnota --comment --body-file C:\tmp\pr-<N>-review.md
gh auth switch --hostname github.com --user ozonev
```

## Gotchas

- **`gh auth status` showing `ozonev` is fine for everything in this skill.** Every call here is read-only against a public repo; only Step 6's `gh pr review --comment` needs `ozonev-hi-fi`, and only the user runs it.
- **"CI is green" answers a narrower question than "the code is right."** CI re-proves build/tests/lint on every run; it says nothing about whether the code matches the ADR, whether a background job's failure path tells anyone, or any other row in Step 4 — those only surface by reading the diff.
- **A long tool result becomes a saved-file pointer, not an error.** Read that file instead of re-requesting the same command with a smaller scope — the content is already there.
