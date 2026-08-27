# hodnota

[![CI Backend](https://github.com/ozonev-hi-fi/hodnota/actions/workflows/ci-backend.yml/badge.svg)](https://github.com/ozonev-hi-fi/hodnota/actions/workflows/ci-backend.yml)
[![CI Web](https://github.com/ozonev-hi-fi/hodnota/actions/workflows/ci-web.yml/badge.svg)](https://github.com/ozonev-hi-fi/hodnota/actions/workflows/ci-web.yml)

Link aggregation / music exchange service.

`hodnota` is a working/draft name (ukr. "годнота", a colloquial Internet term used to describe something that is high-quality, interesting, and worth attention - like a music you like and you want to share with friends). Probably, it is not a final brand.

See [docs/MANIFESTO.md](docs/MANIFESTO.md) for the *why*, [docs/architecture.md](docs/architecture.md) for the current design, [docs/roadmap.md](docs/roadmap.md) for what's next, and [docs/workflow.md](docs/workflow.md) for day-to-day branching/commit commands.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) — version pinned in [`global.json`](global.json).
- [Node.js](https://nodejs.org/) — version pinned in [`web/.nvmrc`](web/.nvmrc). If you use [nvm](https://github.com/nvm-sh/nvm)/[nvm-windows](https://github.com/coreybutler/nvm-windows), see [web/README.md](web/README.md#node-version).

## Backend (`Hodnota.Api` + Clean Architecture solution)

From the repo root:

```
dotnet build                      # build the whole solution
dotnet test                       # run all .NET tests
dotnet run --project src/Hodnota.Api   # run the API locally
dotnet format --verify-no-changes # check formatting (same check CI runs)
```

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
