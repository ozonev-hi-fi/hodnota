# Roadmap

Epic-level plan, checked off as things get done. Task-level work doesn't belong here — this file only tracks the big steps; branch directly for it (see [decisions/0001](decisions/0001-branching-and-versioning-strategy.md) — no GitHub Issue required, one's only worth filing if you want extra tracking).

- [x] Come up with a name for the project (needs a brainstorming session). — kept `hodnota` as a working/draft name, see [decisions/0002-project-name.md](decisions/0002-project-name.md).
- [x] Create/rename the GitHub repository to match the name. - kept the same due to previous decision.
- [x] Design the detailed architecture of the first version (see [architecture.md](architecture.md)) — see [decisions/0003](decisions/0003-initial-architecture.md).
- [x] Scaffold the backend solution (Clean Architecture projects) and the `/web` React app — see [decisions/0004](decisions/0004-scaffold-backend-and-web-app.md).
- [x] Implement auth (Identity) — email + password via ASP.NET Core Identity, bearer tokens, PostgreSQL (prod and local dev container) — see [decisions/0005](decisions/0005-auth-identity.md).
- [x] Add OpenAPI generation + a dev-only interactive API UI (Scalar or similar, gated to Development, not shipped to prod) — see [decisions/0006](decisions/0006-openapi-scalar-dev-ui.md).
- [x] Implement the catalog data model + EF Core migrations (Postgres/SQLite) — see [decisions/0007](decisions/0007-catalog-data-model.md).
- [ ] Implement auth UI (Web) — register/login screens against the Identity API
- [ ] Implement a first streaming provider end-to-end as a walking skeleton
- [ ] Implement the remaining first-release providers
- [ ] Figure out hosting — must be free (or effectively free) to start.
- [ ] Integrate email service
- [ ] Use email service for auth confirmation flows
- [ ] Implement auth (Google/Facebook external login)
- [ ] Sketch/scaffold the MAUI mobile app
- [ ] Implement auth UI (Mobile)
