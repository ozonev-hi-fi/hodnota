# hodnota

[![CI Backend](https://github.com/ozonev-hi-fi/hodnota/actions/workflows/ci-backend.yml/badge.svg)](https://github.com/ozonev-hi-fi/hodnota/actions/workflows/ci-backend.yml)
[![CI Web](https://github.com/ozonev-hi-fi/hodnota/actions/workflows/ci-web.yml/badge.svg)](https://github.com/ozonev-hi-fi/hodnota/actions/workflows/ci-web.yml)

Link aggregation / music exchange service.

`hodnota` is a working/draft name (ukr. "годнота", a colloquial Internet term used to describe something that is high-quality, interesting, and worth attention - like a music you like and you want to share with friends). Probably, it is not a final brand.

See [docs/MANIFESTO.md](docs/MANIFESTO.md) for the *why*, [docs/architecture.md](docs/architecture.md) for the current design, [docs/roadmap.md](docs/roadmap.md) for what's next, and [docs/workflow.md](docs/workflow.md) for day-to-day branching/commit commands.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) — version pinned in [`global.json`](global.json).
- [Node.js](https://nodejs.org/) — version pinned in [`web/.nvmrc`](web/.nvmrc). If you use [nvm](https://github.com/nvm-sh/nvm)/[nvm-windows](https://github.com/coreybutler/nvm-windows), see [web/README.md](web/README.md#node-version).
- [Docker](https://www.docker.com/) (or another compatible engine, e.g. [Rancher Desktop](https://rancherdesktop.io/)) — for the local Postgres dev database ([decisions/0005](docs/decisions/0005-auth-identity.md)) and the Testcontainers-backed integration tests.

## Backend (`Hodnota.Api` + Clean Architecture solution)

From the repo root:

```
dotnet tool restore               # once, after cloning — installs dotnet-ef pinned in .config/dotnet-tools.json
docker compose up -d              # start the local Postgres dev database (persists across restarts)
dotnet build                      # build the whole solution
dotnet test                       # run all .NET tests (the Hodnota.Infrastructure.IntegrationTests project needs Docker)
dotnet run --project src/Hodnota.Api   # run the API locally, against the local Postgres container
dotnet format --verify-no-changes # check formatting (same check CI runs)
```

### Environment configuration (`.env` / `.env.local`)

The root [`.env`](.env) file is committed on purpose — it only holds non-secret local-dev Postgres credentials (localhost-only, never shipped anywhere), read by both `docker-compose.yml` and the API (via `Hodnota.Infrastructure.DotEnvLoader`), so the commands above need no manual configuration. See [decisions/0005](docs/decisions/0005-auth-identity.md).

If you need to override a value locally (a port conflict, a personal API key later on), create a **gitignored** `.env.local` next to it with just the keys you want to change — it's loaded after `.env` and wins on conflicts. Nobody needs one today; it exists for when someone does.

### Migrations

```
dotnet ef migrations add <Name> --project src/Hodnota.Infrastructure --startup-project src/Hodnota.Api -o Identity/Migrations   # add a migration
dotnet ef database update --project src/Hodnota.Infrastructure --startup-project src/Hodnota.Api                                # apply pending migrations manually
dotnet ef migrations remove --project src/Hodnota.Infrastructure --startup-project src/Hodnota.Api                              # undo the last, unapplied migration
```

`dotnet ef` needs the `dotnet-ef` tool from `dotnet tool restore` (above) and a real reachable Postgres — `docker compose up -d` first. `Hodnota.Api` also applies pending migrations automatically on startup (see `Program.cs`), so `dotnet ef database update` is only needed for inspecting/scripting migrations outside running the API.

### API docs

While `dotnet run --project src/Hodnota.Api` is running, open [`http://localhost:5009/scalar/v1`](http://localhost:5009/scalar/v1) (or whatever port you passed via `--urls`) for an interactive, browsable reference of every endpoint — including a working "Authorize" button for the bearer tokens `/api/auth/login` issues. The raw OpenAPI document backing it is at `/openapi/v1.json`. Both are Development-only ([decisions/0006](docs/decisions/0006-openapi-scalar-dev-ui.md)) and won't respond at all (404) outside that environment.

## Web (`/web`, React + TypeScript)

From `/web` (see [web/README.md](web/README.md) for the full rundown):

```
npm install
npm run dev      # start the dev server
npm test         # run tests
npm run build    # production build
npm run check    # lint/format check (same check CI runs)
```

## CI

Two independent workflows, each running only when a PR touches the paths it cares about: [`.github/workflows/ci-backend.yml`](.github/workflows/ci-backend.yml) (the backend checks above) and [`.github/workflows/ci-web.yml`](.github/workflows/ci-web.yml) (the web checks above). See [decisions/0004](docs/decisions/0004-scaffold-backend-and-web-app.md) for the reasoning.
