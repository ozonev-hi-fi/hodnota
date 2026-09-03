# 0006. OpenAPI generation + dev-only Scalar UI

Status: accepted

## Context

[roadmap.md](../roadmap.md)'s next open item after auth: "Add OpenAPI generation + a dev-only interactive API UI (Scalar or similar, gated to Development, not shipped to prod)" — general tooling for every endpoint from here on, deliberately landing before the catalog data model and streaming-provider work that will most benefit from having it.

[architecture.md](../architecture.md)'s Open Questions already carried "OpenAPI generation for typed clients" as unresolved. This ADR settles the document-generation-and-dev-UI half of that; generating a typed TypeScript client for `/web` from the resulting document is a separate, still-open question — see Consequences.

`Hodnota.Api` is minimal-API-only (`dotnet new web`, no controllers — [decisions/0004](0004-scaffold-backend-and-web-app.md)); the only endpoints today are ASP.NET Core Identity's built-in group, mounted via `app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>()` ([decisions/0005](0005-auth-identity.md)). Only its `/manage/*` sub-group is actually bearer-protected (`RequireAuthorization()`, applied internally by `MapIdentityApi`); `/register`, `/login`, `/refresh`, `/confirmEmail`, `/resendConfirmationEmail`, `/forgotPassword`, and `/resetPassword` are all anonymous by design — any OpenAPI security-scheme wiring has to reflect that per-endpoint split, not treat the group as uniformly protected.

## Decision

### Document generator: built-in `Microsoft.AspNetCore.OpenApi`

.NET 9+ ships a first-party OpenAPI document generator (`AddOpenApi()` / `MapOpenApi()`) that works directly against minimal API endpoint metadata, including `MapIdentityApi`'s built-in group, with no extra reflection-heavy generator in the request pipeline. On .NET 10 this is Microsoft's current recommended path for minimal-API projects, so there's no reason to add Swashbuckle (which targets controller-attribute annotation as its primary model, with minimal-API support layered on) as a second, competing document generator.

### Interactive UI: `Scalar.AspNetCore`

`Scalar.AspNetCore`'s `app.MapScalarApiReference()` is a thin UI layer that reads the same document `MapOpenApi()` produces — it isn't a second spec generator, unlike wiring up Swagger UI would effectively be alongside the built-in generator. It's also the option the roadmap names explicitly. Chosen over Swagger UI for that reason, and because it needs no extra document-format shim to consume the built-in generator's output.

### Gated to Development only, via `IsDevelopment()` — no separate flag

Both `app.MapOpenApi()` and `app.MapScalarApiReference()` are called only inside `if (app.Environment.IsDevelopment())` in `Program.cs` — the first environment branch introduced in that file. `ASPNETCORE_ENVIRONMENT` is already the project's environment mechanism (e.g. `appsettings.Development.json`'s existence); a second config flag to gate the same thing would be redundant. This satisfies the roadmap's explicit "not shipped to prod" requirement: in any non-Development environment, the document and UI routes simply don't exist (404, not merely hidden/unauthenticated).

### Bearer security scheme declared per-operation, not document-wide

Without a declared security scheme, Scalar's UI has no "Authorize" control and protected endpoints have to be tried with a manually-crafted `Authorization` header. Two transformers, both in `Hodnota.Api`, registered via `AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>().AddOperationTransformer<BearerAuthOperationTransformer>())`:

- `BearerSecuritySchemeTransformer` (`IOpenApiDocumentTransformer`) adds the HTTP-bearer scheme once, to `Components.SecuritySchemes`.
- `BearerAuthOperationTransformer` (`IOpenApiOperationTransformer`) attaches the security *requirement* to only the operations whose endpoint carries `IAuthorizeData` (i.e. `RequireAuthorization()`'d) — in practice, today, just `/manage/2fa` and `/manage/info`.

A document-level `document.Security` entry was tried first and rejected: OpenAPI semantics make a document-level security requirement apply to every operation that doesn't declare its own, which would have mismarked `/register`, `/login`, `/refresh`, and the rest as requiring a bearer token in the generated doc and the Scalar UI, when they don't. This is presentation-layer, request-pipeline-adjacent glue, not business logic — it lives in `Hodnota.Api` alongside `Program.cs`, not in `Hodnota.Infrastructure`.

### Routes: framework defaults

`/openapi/v1.json` for the document, `/scalar/v1` for the UI — both are `Microsoft.AspNetCore.OpenApi`'s and Scalar's out-of-the-box defaults. No reason to customize either path yet; revisit only if a second document (e.g. a versioned v2) is ever needed.

## Consequences

- Every future minimal-API endpoint gets picked up by the OpenAPI document automatically — no per-endpoint opt-in step, matching the roadmap's framing of this as "general tooling... not specific to any one feature."
- [architecture.md](../architecture.md)'s Tooling & Conventions section gets a line documenting this; its Open Questions entry narrows from "OpenAPI generation for typed clients" to just the typed-client-generation half, since document generation and the dev UI are now built.
- [roadmap.md](../roadmap.md)'s item is checked off, linking here.
- Deliberately deferred: generating a typed TypeScript client for `/web` from the OpenAPI document. Nothing in `/web` consumes the API via a generated client yet, and adding one now would be speculative — it becomes its own roadmap item if/when `/web` needs it.
- No CORS changes: the dev UI is served same-origin from the API itself (not from the separate Vite dev server), so no cross-origin wiring is needed for it to work locally.
- Anyone hitting `/openapi/v1.json` or `/scalar/v1` against a Production-configured host gets a 404, not an auth prompt or an empty page — there is no code path that maps those routes outside `IsDevelopment()`.
