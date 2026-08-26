# 0004. Scaffold the backend and web app

Status: in progress

## Context

[roadmap.md](../roadmap.md)'s next open item is "Scaffold the backend solution (Clean Architecture projects) and the `/web` React app." [decisions/0003](0003-initial-architecture.md) already fixed *what* gets built — the project list, the tooling choices — but left several scaffold-time execution questions open: how much runtime behavior (if any) the scaffold should prove, whether the four test projects from 0003's tree get created before there's any logic to test, whether `.slnx` actually works with the installed toolchain, and how the work gets committed. These are individually small but easy to silently re-litigate later ("why is there no health endpoint", "why are the test projects empty"), so they're pinned down here before any scaffolding code is written.

## Decision

- **Scope: bare skeleton only.** No endpoints beyond what compiles by default, no pages beyond the Vite template default, no dev-proxy wiring. The roadmap already reserves "walking skeleton" (a real API↔UI↔DB proof) for a later, separate item ("Implement a first streaming provider end-to-end as a walking skeleton") — this step doesn't preempt that.
- **Test projects created now, not deferred.** All four from 0003's tree (`Hodnota.Domain.Tests`, `.Application.Tests`, `.Infrastructure.Tests`, `.Api.Tests`) are wired into the solution now, each with one trivial placeholder test (e.g. `true.Should().BeTrue()`) rather than left with zero tests — some CI runners (GitHub Actions included) treat "no tests found" as a failing exit code, not a pass. Placeholders get replaced by real tests as each layer gets logic.
- **`.slnx` attempted first**, per 0003; falls back to a classic `.sln` without further ceremony if the installed SDK/IDE combo doesn't cooperate (SDK 10.0.400 is installed).
- **SDK pin:** root `global.json` pinning to the installed .NET SDK (10.0.400), `rollForward: latestMajor` — guarantees a floor without blocking newer installs, matching 0003's latest-stable-everything policy.
- **Root `Directory.Build.props`** centralizing `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `AnalysisLevel latest` across every .NET project, avoiding repeating these across 9 projects.
- **`Hodnota.Api` scaffolded via `dotnet new web`** (the empty ASP.NET Core template), not `webapi` — avoids pulling in the `WeatherForecast` sample entirely rather than scaffolding it and deleting it. Its single default `GET /` sample endpoint is also removed, so the API truly has zero endpoints for now.
- **CI foundation added now, not backfilled later.** `.github/workflows/ci.yml` with two independent top-level jobs (no `needs` between them, so GitHub runs them in parallel): `backend` (`dotnet restore` → `dotnet format --verify-no-changes` → build → test) and `web` (`npm ci` → `biome check` → `vite build`). Both on `ubuntu-latest`, matching the Linux-container hosting direction in [architecture.md](../architecture.md). Triggers: `pull_request` (any base branch) + `push` to `develop`/`main`.
  - `actions/setup-dotnet` reads the SDK version from the root `global.json` (`global-json-file` input); `actions/setup-node` reads from a new `web/.nvmrc` (added during the web-scaffold step) via `node-version-file` — one source of truth each, no duplicate version pins.
  - Built-in caching only (`setup-dotnet`'s `cache: true`, `setup-node`'s `cache: npm`) — no hand-rolled `actions/cache`.
  - A `concurrency` group per ref cancels superseded runs; workflow-level `permissions: contents: read` as a minimal-privilege default.
  - No path filtering (both jobs always run regardless of what changed) — kept simple until that's an actual pain point.
  - No branch-protection/required-status-check is enabled by this ADR — [workflow.md](../workflow.md)'s "no review gate, solo repo" stance stands; CI is advisory for now. Explicit open item, not a silent gap.
  - Nothing schema-touching exists yet, so no Postgres/Testcontainers wiring is needed in this pipeline yet; `ubuntu-latest` ships Docker preinstalled, so nothing extra will be needed there when it's added, per 0003.
  - Resolves [architecture.md](../architecture.md)'s open item "CI pipeline (build/test/lint on every PR)" — that commit removes it from Open Questions and links back here.
- **Four commits on this one feature branch:** this ADR, then the backend scaffold, then the web scaffold, then the CI foundation — each independently buildable/runnable.

## Implementation plan

Tracked here as a living checklist for this feature branch. After each commit's changes are made, all commits made so far on this branch are reviewed together as one unit (code review + check against this plan) before the step below is removed and work starts on the next one. Once the list is empty, Status flips to `accepted` — that's the branch's own signal that it's ready to merge into `develop`.

- [ ] **Web scaffold** — `/web` via `npm create vite@latest web -- --template react-ts`, kept close to the generated starter (bare skeleton, no proxy/API wiring); Biome added for lint/format (`biome.json`, `lint`/`format` scripts); `web/.nvmrc` added (also doubles as the CI job's Node version source). Update [roadmap.md](../roadmap.md) to check off this item. Verify: `npm run build`, `npm run dev`, `npx biome check .`.
- [ ] **CI foundation** — `.github/workflows/ci.yml` with the parallel `backend`/`web` jobs described above. Update [architecture.md](../architecture.md): remove "CI pipeline (build/test/lint on every PR)" from Open Questions, add a short CI note linking back here. Verify: push the branch (or open the PR) and confirm both jobs go green in the Actions tab. This is also the step where the checklist above empties out and `Status` flips from `in progress` to `accepted`.

## Consequences

- Scaffold commits will look "empty" (no real functionality) by design — that's intentional, not incompleteness. Health-endpoint/proxy wiring is deliberately deferred to the walking-skeleton roadmap item.
- Placeholder tests are a temporary stand-in and need to be swapped for real ones as logic lands, not left as permanent green noise.
- `.slnx` fallback to `.sln` is an accepted, reversible risk per 0003.
- CI lands as advisory only (no required status checks / branch protection) — its value right now is fast feedback, not a merge gate. Revisit if that stops being sufficient.
- This is the first ADR used as a live in-branch implementation tracker (Status `in progress` → `accepted`, checklist edited down commit by commit) rather than a point-in-time record written after the fact. If this pattern proves useful it's worth promoting into [workflow.md](../workflow.md) as a general practice; not done here since it's only been tried once so far.
