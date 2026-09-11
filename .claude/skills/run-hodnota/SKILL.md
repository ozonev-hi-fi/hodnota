---
name: run-hodnota
description: Build, run, and drive hodnota (Hodnota.Api backend + web frontend). Use when asked to start hodnota, run its tests, build it, or exercise its auth/search/share flows end-to-end.
---

hodnota is a .NET 10 API (`Hodnota.Api`, ASP.NET Core Identity auth under `/api/auth`, catalog search/share under `/api/catalog`) plus a React/Vite web frontend (`/web`) with a real UI: login, register, forgot/reset password, confirm email, change password, a search page, and a share page — all wired to the real backend via a typed `openapi-fetch` client, proxied through Vite's dev server. Drive the backend via `bash .claude/skills/run-hodnota/smoke.sh`, which launches the API against a dedicated `hodnota_agent` database (never the developer's own `hodnota`, see Cleanup below) and exercises the auth endpoints with `curl`. All paths below are relative to the repo root.

The local Postgres container actually holds two databases (`hodnota` = the developer's own persistent dataset, `hodnota_agent` = agent/manual verification scratch space, both provisioned automatically by `docker/postgres-init/` on first container init) — anything agent-driven should target `hodnota_agent`, never `hodnota`.

Verified in this session on **Windows (git-bash + PowerShell)**. The driver auto-detects `powershell.exe` and falls back to `lsof` for process cleanup on Linux/macOS, but that fallback path has not been run here.

## Prerequisites

- .NET SDK — pinned in `global.json` (`10.0.400`, `rollForward: latestMajor`, so a newer installed 10.x SDK resolves fine — confirmed working on 10.0.401).
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

The driver is `.claude/skills/run-hodnota/smoke.sh`. It starts the local Postgres container, launches `Hodnota.Api` (against `hodnota_agent`) in the background, polls until it's actually accepting connections, then runs a real register -> confirm email -> login -> authenticated-call -> refresh flow against it, and cleans up (stops the API, deletes the test user) on exit either way.

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
== confirm email ==
Thank you for confirming your email.
HTTP:200
== login ==
{"tokenType":"Bearer","accessToken":"...","expiresIn":3600,"refreshToken":"..."}
== authenticated manage/info ==
{"email":"smoke-1789025954@example.com","isEmailConfirmed":true}
HTTP:200
== refresh ==
{"tokenType":"Bearer","accessToken":"...","expiresIn":3600,"refreshToken":"..."}
HTTP:200
Cleaning up smoke-test user...
Smoke test passed.
Stopping API (port 5299)...
```

### Web frontend

There's still no browser-automation tool (`chromium-cli`/Playwright) available in this environment, so verification stays HTTP-level, not a screenshot or real keyboard/click interaction — but the app now has real routes and real API calls to exercise this way, not just a static shell:

```bash
docker compose up -d
nohup bash .claude/skills/run-hodnota/run-agent-api.sh > /tmp/hodnota-api-dev.log 2>&1 &
cd web && nohup npm run dev > /tmp/hodnota-web-dev.log 2>&1 &
```

`run-agent-api.sh` launches the API against `hodnota_agent`, not the developer's own `hodnota` — always use it (or its `ConnectionStrings__Default` override directly) for agent-driven runs instead of a plain `dotnet run --project src/Hodnota.Api`, which defaults to the developer's database.

Then drive real flows through the Vite dev server's own address (port 5173) — its proxy (`web/vite.config.ts`) forwards anything under `/api` to the backend, the same path a real browser takes:

```bash
# SPA shell
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5173/

# register -> confirm email -> login -> authenticated call, all through the proxy
EMAIL="e2e-$(date +%s)@example.com"
curl -s -X POST http://localhost:5173/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"P@ssw0rd!123\"}"
# login requires a confirmed email — extract userId/code from the logged link (see Notes below)
sleep 1
QUERY=$(grep "Confirmation link for $EMAIL" /tmp/hodnota-api-dev.log | tail -1 | grep -oE '\?userId=.*')
curl -s "http://localhost:5173/api/auth/confirmEmail${QUERY}"
TOKEN=$(curl -s -X POST http://localhost:5173/api/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"P@ssw0rd!123\"}" | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)
curl -s -X POST http://localhost:5173/api/catalog/search -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" -d '{"search":"nothing else matters"}'
```

Notes specific to this flow:
- Login requires a confirmed email (`decisions/0009-auth-ui-search-share-web.md`) — `forgotPassword` also silently no-ops (200, but nothing sent) for an unconfirmed account.
- `NoOpEmailSender` logs the confirmation link (rewritten to point at the SPA's `/confirm-email`, not the raw API — same ADR) and the password-reset **code** (not a link — there's no URL to click for reset) to the API log; grep it for `Confirmation link for` / `Password reset code for`.
- `POST /api/catalog/search` and `/resolve` require the `Authorization` header (401 without it); `GET /api/catalog/sharepages/{id}` is deliberately anonymous — a real check is confirming that one works *without* the header.
- Real search results come back from the actual YouTube Data API (not stubbed) when a valid `YouTube:ApiKey` is configured — expect real video titles/artists in the response.

Stop both dev processes by port (`Get-NetTCPConnection -LocalPort 5009`/`5173` on Windows, `lsof` elsewhere) — see the Gotcha below about why matching by process name is riskier than it looks. This flow only ever touches `hodnota_agent` (via `run-agent-api.sh`) — see Cleanup below before finishing regardless, since `hodnota_agent` still accumulates its own cruft over time.

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
dotnet test    # 99 tests across 5 projects; Hodnota.Infrastructure.IntegrationTests needs Docker (Testcontainers)
cd web && npm test   # 64 tests across 17 files (Vitest + React Testing Library)
```

None of this touches the persistent local dev database (SQLite in-memory or Testcontainers-Postgres, both ephemeral) — only manual verification does. See Cleanup below.

## Cleanup: reset hodnota_agent after manual verification

The local dev Postgres container keeps its data across restarts by design (a named Docker volume — see `docs/architecture.md`'s Database section), so anything created by manual verification (registering a user via curl or the browser, running a search, etc.) stays there permanently otherwise. **Never run `docker compose down -v`** to deal with this — it wipes the volume entirely, taking the developer's own persistent `hodnota` database down with it. Since agent/manual verification only ever targets `hodnota_agent` (see `run-agent-api.sh` above), reset just that one database:

```bash
docker exec hodnota-postgres-1 psql -U hodnota -d hodnota -c "DROP DATABASE hodnota_agent WITH (FORCE);"
docker exec hodnota-postgres-1 psql -U hodnota -d hodnota -c "CREATE DATABASE hodnota_agent OWNER hodnota;"
```

(Connect to `hodnota`, not `hodnota_agent`, to run the drop — Postgres refuses to drop the database you're currently connected to.) The API reapplies migrations automatically on its next start against `hodnota_agent` (`Program.cs`'s `Database.MigrateAsync()` for the Postgres provider), so nothing else is needed — no manual migration step, no seed script.

Do this before finishing any task that included manual verification through `run-agent-api.sh` or the browser against `hodnota_agent` — don't leave test users for the next session to find. `smoke.sh` doesn't need this: it already deletes its own one test user on exit, regardless of pass/fail.

---

## Gotchas

- **A .NET listener on Windows owns both an IPv4 and IPv6 socket for the same port.** `Get-NetTCPConnection -LocalPort $PORT | Select-Object OwningProcess` returns the same PID twice. Feeding that straight into `Stop-Process -Id $pids` breaks — the embedded newline turns it into an invalid multi-line `-Id` argument, and the failure is silent if you've wrapped it in `|| true`. Dedupe (`sort -u`) before looping over the PIDs.
- **Don't parse `%{http_code}` with a `|| echo "000"` fallback for a readiness check.** On a connection failure, curl still writes its own `"000"` placeholder for `%{http_code}` *and* the shell fallback fires, concatenating into `"000000"` — which is `!= "000"`, so a readiness loop checking that condition reports "ready" on the very first, still-down attempt. Check curl's own exit code instead (`if curl ...; then`) — 0 means it got a real HTTP response, non-zero means it couldn't connect at all.
- **An `EXIT` trap under `set -e` can silently overwrite a passing run's exit code.** If any command inside the trap handler returns non-zero, the *trap's* exit status becomes the script's final exit status — a `PASS` can report as exit 1 with no visible error. Capture `$?` as the very first line of the handler and `exit` with it explicitly at the end.
- **A backgrounded `npm run dev &> file &` can silently never start** (no process, no log content) depending on exactly how it's launched; wrapping it in `nohup` with an explicit absolute log path reliably worked here when a bare redirect didn't. Cause unconfirmed — treat it as "use `nohup` + absolute path for backgrounded npm processes" rather than a solved mystery.
- **`dotnet-ef`'s design-time build runs from the startup project's output directory** (`src/Hodnota.Api/bin/Debug/net10.0`), not wherever you invoked `dotnet ef` from — a relative `.env` lookup that only checks the current directory misses it. `Hodnota.Infrastructure.DotEnvLoader` searches upward (`DotNetEnv`'s `Env.TraversePath()`) specifically because of this.
- **Stopping the Vite dev server by matching `Get-Process -Name node` is riskier than it looks.** That matches *every* Node process on the machine, not just Vite — including unrelated background tools (other dev servers, editor extensions, etc.). One session here ran `Get-Process -Name node | Stop-Process -Force` to clean up and it silently killed all Node processes system-wide. Find the specific PID by port instead (`Get-NetTCPConnection -LocalPort 5173 | Select-Object -ExpandProperty OwningProcess`) and stop only that.

## Troubleshooting

- **`Missing 'ConnectionStrings__Default'`** thrown by `ApplicationDbContextFactory` (during `dotnet ef ...`): the repo-root `.env` wasn't found, or you're running from somewhere `TraversePath()`'s upward search can't reach. Confirm `.env` exists at the repo root.
- **Smoke script's `register` call gets `curl: (7) Failed to connect`** even though `Waiting for readiness...` printed `Ready.`: almost certainly the `%{http_code}`-parsing readiness bug above (already fixed in this script) rather than the app actually being slow — if you see this after editing the readiness loop, check for that pattern first.
- **A stale `Hodnota.Api.exe` still listening after a script "successfully" stopped it**: happened repeatedly during heavy iterative debugging in this session with many accumulated `dotnet`/build-server processes. `Get-Process -Name Hodnota.Api,dotnet | Stop-Process -Force` clears it; genuinely check `Get-NetTCPConnection -LocalPort <port>` returns nothing before concluding the app itself is broken.
- **`dotnet build`/`dotnet run` suddenly fails with "A compatible .NET SDK was not found"** even though it worked minutes earlier: check `dotnet --list-sdks` — if the pinned major version has genuinely disappeared, something external (Windows Update, Visual Studio Installer, etc.) is mid-change on the machine, not a repo problem. Confirmed once in this session; resolved itself after a reboot.
