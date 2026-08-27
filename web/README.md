# hodnota web UI

React + TypeScript, built with [Vite](https://vite.dev/). See [../docs/architecture.md](../docs/architecture.md) for how this fits into the rest of the project (in production it's built and served by `Hodnota.Api`).

## Node version

The required Node version is pinned in [`.nvmrc`](.nvmrc) (currently the latest LTS). If you manage Node with [nvm](https://github.com/nvm-sh/nvm) on macOS/Linux, run this in this directory before `npm install` — it reads `.nvmrc` automatically:

```
nvm install
nvm use
```

On Windows with [nvm-windows](https://github.com/coreybutler/nvm-windows), `.nvmrc` isn't read automatically — pass the version from the file explicitly:

```
nvm install 24
nvm use 24
```

## Scripts

- `npm run dev` — start the dev server (hot module reload).
- `npm run build` — type-check (`tsc -b`) and build for production into `dist/`.
- `npm run lint` — lint with [Biome](https://biomejs.dev/).
- `npm run format` — format with Biome.
- `npm run preview` — locally preview a production build.
