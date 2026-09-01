---
name: run-hodnota
description: Build, run, and drive hodnota (Hodnota.Api backend + web frontend). Use when asked to start hodnota, run its tests, build it, or exercise its auth endpoints (register/login/refresh) end-to-end.
---

hodnota is a .NET 10 API (`Hodnota.Api`, ASP.NET Core Identity auth under `/api/auth`) plus a React/Vite web frontend (`/web`, currently a bare placeholder page — no real UI yet). Drive the backend via `bash .claude/skills/run-hodnota/smoke.sh`, which launches the API against the local Postgres container and exercises the auth endpoints with `curl`. All paths below are relative to the repo root.

Verified in this session on **Windows (git-bash + PowerShell)**. The driver auto-detects `powershell.exe` and falls back to `lsof` for process cleanup on Linux/macOS, but that fallback path has not been run here.

## Prerequisites

- .NET SDK — version pinned in `global.json` (10.0.400 at last check).
- Node.js — version pinned in `web/.nvmrc` (24).
- Docker (Rancher Desktop on this machine) — for the local Postgres dev container.

## Setup

```bash
dotnet tool restore     # installs dotnet-ef, pinned in .config/dotnet-tools.json
docker compose up -d    # starts the local dev Postgres (reads the committed .env — see README.md)
cd web && npm install && cd ..
```

## Build

```bash
dotnet build
cd web && npm run build && cd ..   # tsc -b && vite build, output -> web/dist
```

## Run (agent path)

The driver is `.claude/skills/run-hodnota/smoke.sh`. It starts the local Postgres container, launches `Hodnota.Api` in the background, polls until it's actually accepting connections, then runs a real register -> login -> authenticated-call -> refresh flow against it, and cleans up (stops the API, deletes the test user) on exit either way.

```bash
bash .claude/skills/run-hodnota/smoke.sh
```

Override the port with `PORT=5300 bash .claude/skills/run-hodnota/smoke.sh`. API log lands at `${TMPDIR:-/tmp}/hodnota-api-smoke.log`. Exit code reflects the actual test result (0 = passed) even though cleanup always runs.

Sample output from a real run:

```
Starting local dev Postgres...
Launching Hodnota.Api on http://localhost:5299 (log: /tmp/hodnota-api-smoke.log)...
Waiting for readiness...
Ready.
== register ==

HTTP:200
== login ==
{"tokenType":"Bearer","accessToken":"...","expiresIn":3600,"refreshToken":"..."}
== authenticated manage/info ==
{"email":"smoke-1788251455@example.com","isEmailConfirmed":false}
HTTP:200
== refresh ==
{"tokenType":"Bearer","accessToken":"...","expiresIn":3600,"refreshToken":"..."}
HTTP:200
Cleaning up smoke-test user...
Smoke test passed.
Stopping API (port 5299)...
```

### Web frontend

There's no browser-automation tool (`chromium-cli`) available in this environment, and the page itself is currently just a placeholder heading (`<h1>hodnota</h1>`) with nothing to click — so verification here is HTTP-level, not a screenshot:

```bash
cd web && npm run dev > /tmp/hodnota-web.log 2>&1 &
sleep 3
curl -s http://localhost:5173/   # -> full HTML shell, <title>hodnota</title>, mounts src/main.tsx
```

Stop it the same way as the API (find the PID on port 5173 via `Get-NetTCPConnection`/`lsof`, then kill it). Revisit this section — add a real `chromium-cli`/Playwright driver — once actual UI exists to click through; a placeholder page isn't worth automating yet.

## Run (human path)

```bash
docker compose up -d
dotnet run --project src/Hodnota.Api   # http://localhost:5009, ASPNETCORE_ENVIRONMENT=Development via launchSettings.json
```

```bash
cd web && npm run dev   # http://localhost:5173, Ctrl-C to stop
```

## Test

```bash
dotnet test    # 12 tests across 5 projects; Hodnota.Infrastructure.IntegrationTests needs Docker (Testcontainers)
cd web && npm test
```

---

## Gotchas

- **A .NET listener on Windows owns both an IPv4 and IPv6 socket for the same port.** `Get-NetTCPConnection -LocalPort $PORT | Select-Object OwningProcess` returns the same PID twice. Feeding that straight into `Stop-Process -Id $pids` breaks — the embedded newline turns it into an invalid multi-line `-Id` argument, and the failure is silent if you've wrapped it in `|| true`. Dedupe (`sort -u`) before looping over the PIDs.
- **Don't parse `%{http_code}` with a `|| echo "000"` fallback for a readiness check.** On a connection failure, curl still writes its own `"000"` placeholder for `%{http_code}` *and* the shell fallback fires, concatenating into `"000000"` — which is `!= "000"`, so a readiness loop checking that condition reports "ready" on the very first, still-down attempt. Check curl's own exit code instead (`if curl ...; then`) — 0 means it got a real HTTP response, non-zero means it couldn't connect at all.
- **An `EXIT` trap under `set -e` can silently overwrite a passing run's exit code.** If any command inside the trap handler returns non-zero, the *trap's* exit status becomes the script's final exit status — a `PASS` can report as exit 1 with no visible error. Capture `$?` as the very first line of the handler and `exit` with it explicitly at the end.
- **A backgrounded `npm run dev &> file &` can silently never start** (no process, no log content) depending on exactly how it's launched; wrapping it in `nohup` with an explicit absolute log path reliably worked here when a bare redirect didn't. Cause unconfirmed — treat it as "use `nohup` + absolute path for backgrounded npm processes" rather than a solved mystery.
- **`dotnet-ef`'s design-time build runs from the startup project's output directory** (`src/Hodnota.Api/bin/Debug/net10.0`), not wherever you invoked `dotnet ef` from — a relative `.env` lookup that only checks the current directory misses it. `Hodnota.Infrastructure.DotEnvLoader` searches upward (`DotNetEnv`'s `Env.TraversePath()`) specifically because of this.

## Troubleshooting

- **`Missing 'ConnectionStrings__Default'`** thrown by `ApplicationDbContextFactory` (during `dotnet ef ...`): the repo-root `.env` wasn't found, or you're running from somewhere `TraversePath()`'s upward search can't reach. Confirm `.env` exists at the repo root.
- **Smoke script's `register` call gets `curl: (7) Failed to connect`** even though `Waiting for readiness...` printed `Ready.`: almost certainly the `%{http_code}`-parsing readiness bug above (already fixed in this script) rather than the app actually being slow — if you see this after editing the readiness loop, check for that pattern first.
- **A stale `Hodnota.Api.exe` still listening after a script "successfully" stopped it**: happened repeatedly during heavy iterative debugging in this session with many accumulated `dotnet`/build-server processes. `Get-Process -Name Hodnota.Api,dotnet | Stop-Process -Force` clears it; genuinely check `Get-NetTCPConnection -LocalPort <port>` returns nothing before concluding the app itself is broken.
