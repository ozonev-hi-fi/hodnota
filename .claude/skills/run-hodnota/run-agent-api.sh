#!/usr/bin/env bash
# Launches Hodnota.Api against hodnota_agent — a separate database from the developer's own
# `hodnota` (created automatically alongside it, see docker/postgres-init/) — so agent/manual
# verification (registering test users, running searches, etc.) never touches personal data.
# Values below mirror .env's committed, non-secret local-only Postgres credentials — update here
# if .env's ever change.
#
# Usage: bash .claude/skills/run-hodnota/run-agent-api.sh [dotnet run args...]
set -euo pipefail

export ConnectionStrings__Default="Host=localhost;Port=5433;Database=hodnota_agent;Username=hodnota;Password=hodnota"

exec dotnet run --project src/Hodnota.Api "$@"
