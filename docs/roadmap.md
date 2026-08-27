# Roadmap

Epic-level plan, checked off as things get done. Task-level work doesn't belong here — this file only tracks the big steps; branch directly for it (see [decisions/0001](decisions/0001-branching-and-versioning-strategy.md) — no GitHub Issue required, one's only worth filing if you want extra tracking).

- [x] Come up with a name for the project (needs a brainstorming session). — kept `hodnota` as a working/draft name, see [decisions/0002-project-name.md](decisions/0002-project-name.md).
- [x] Create/rename the GitHub repository to match the name. - kept the same due to previous decision.
- [x] Design the detailed architecture of the first version (see [architecture.md](architecture.md)) — see [decisions/0003](decisions/0003-initial-architecture.md).
- [ ] Scaffold the backend solution (Clean Architecture projects) and the `/web` React app — see [decisions/0004](decisions/0004-scaffold-backend-and-web-app.md) (in progress).
- [ ] Implement auth (Identity)
- [ ] Implement the catalog data model + EF Core migrations (Postgres/SQLite)
- [ ] Implement a first streaming provider end-to-end as a walking skeleton
- [ ] Implement the remaining first-release providers
- [ ] Figure out hosting — must be free (or effectively free) to start.
- [ ] Implement auth (Google/Facebook external login)
- [ ] Sketch/scaffold the MAUI mobile app
