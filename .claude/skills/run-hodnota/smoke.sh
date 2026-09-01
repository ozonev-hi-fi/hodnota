#!/usr/bin/env bash
# Builds/launches Hodnota.Api and drives its Identity auth endpoints end-to-end.
# Run from the repo root: bash .claude/skills/run-hodnota/smoke.sh
set -euo pipefail

PORT="${PORT:-5299}"
BASE_URL="http://localhost:${PORT}"
LOG_FILE="${TMPDIR:-/tmp}/hodnota-api-smoke.log"

cleanup() {
  # Capture the real exit code first — an EXIT trap's own status otherwise overrides it,
  # silently turning a passing run into a "failure" if any cleanup command here returns non-zero.
  local exit_code=$?
  echo "Stopping API (port $PORT)..."
  if command -v powershell.exe >/dev/null 2>&1; then
    # A .NET listener on Windows usually owns both an IPv4 and IPv6 socket, so this can return
    # the same PID twice — dedupe, or a multi-line value breaks the Stop-Process command string.
    pids=$(powershell.exe -NoProfile -Command "Get-NetTCPConnection -LocalPort $PORT -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess" 2>/dev/null | tr -d '\r' | sort -u)
    for p in $pids; do
      powershell.exe -NoProfile -Command "Stop-Process -Id $p -Force" >/dev/null 2>&1 || true
    done
  else
    lsof -ti:"$PORT" -sTCP:LISTEN 2>/dev/null | xargs -r kill || true
  fi
  exit "$exit_code"
}
trap cleanup EXIT

echo "Starting local dev Postgres..."
docker compose up -d

echo "Launching Hodnota.Api on $BASE_URL (log: $LOG_FILE)..."
(cd src/Hodnota.Api && ASPNETCORE_ENVIRONMENT=Development nohup dotnet run --no-launch-profile --urls "$BASE_URL" >"$LOG_FILE" 2>&1 &)

echo "Waiting for readiness..."
ready=""
for _ in $(seq 1 60); do
  # Check curl's own exit code (0 = got *some* HTTP response, non-zero = couldn't connect at all) —
  # do not parse %{http_code} for this: on a connection failure curl still prints its "000"
  # placeholder, so concatenating it with a `|| echo "000"` fallback silently produces "000000",
  # which is `!= "000"` and falsely looks "ready" on the very first, still-down attempt.
  if curl -s -o /dev/null -X POST "$BASE_URL/api/auth/login" -H "Content-Type: application/json" -d '{}'; then
    ready=1
    break
  fi
  sleep 1
done
if [ -z "$ready" ]; then
  echo "API never came up. Last 40 log lines:"
  tail -n 40 "$LOG_FILE"
  exit 1
fi
echo "Ready."

email="smoke-$(date +%s)@example.com"
password='P@ssw0rd!123'

echo "== register =="
curl -sS -w '\nHTTP:%{http_code}\n' -X POST "$BASE_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$email\",\"password\":\"$password\"}"

echo "== login =="
login_response=$(curl -sS -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$email\",\"password\":\"$password\"}")
echo "$login_response"
access_token=$(echo "$login_response" | grep -oE '"accessToken":"[^"]+"' | cut -d'"' -f4)
refresh_token=$(echo "$login_response" | grep -oE '"refreshToken":"[^"]+"' | cut -d'"' -f4)

echo "== authenticated manage/info =="
curl -sS -w '\nHTTP:%{http_code}\n' "$BASE_URL/api/auth/manage/info" \
  -H "Authorization: Bearer $access_token"

echo "== refresh =="
curl -sS -w '\nHTTP:%{http_code}\n' -X POST "$BASE_URL/api/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$refresh_token\"}"

echo "Cleaning up smoke-test user..."
docker exec hodnota-postgres-1 psql -U hodnota -d hodnota -c "DELETE FROM \"AspNetUsers\" WHERE \"Email\" = '$email';" >/dev/null

echo "Smoke test passed."
