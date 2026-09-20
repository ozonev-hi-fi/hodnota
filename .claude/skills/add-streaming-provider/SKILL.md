---
name: add-streaming-provider
description: Adds a new streaming/purchase platform (Qobuz, Tidal, Deezer, Apple Music, Bandcamp, etc.) to hodnota's catalog search by walking through the mechanical wiring every `IStreamingProvider` needs and the vendor-specific research each one requires before writing code. Use when picking up a roadmap item like "implement Qobuz" or "add Tidal search", or when asked to wire up a new music-platform search source.
---

hodnota has two `IStreamingProvider` implementations today — YouTube (`Hodnota.Infrastructure.Providers.YouTube`, official SDK) and Spotify (`Hodnota.Infrastructure.Providers.Spotify`, raw `HttpClient`) — and [docs/roadmap.md](../../../docs/roadmap.md) lists four more planned: Qobuz, Tidal, Deezer, Apple Music. Adding Spotify repeated most of what YouTube had already established; this skill captures that repeatable part (provider codes, trust order, DI registration, config keys, appsettings, tests, docs) so each new provider only has to supply the genuinely new part (the vendor's own client, DTOs, and mapping logic). [decisions/0008](../../../docs/decisions/0008-youtube-search-sharepage-skeleton.md) and [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md) already settled *why* the architecture looks like this — this skill doesn't re-derive that reasoning, it links to it and tells you which file to touch and in what order.

Per [docs/roadmap.md](../../../docs/roadmap.md)'s "Implement the remaining first-release providers" item, each provider is its own `feature/<short-description>` branch off `develop`, merged one at a time — don't bundle two providers into one branch. Check the current branch against `CLAUDE.md`'s branching rule before starting.

## Prerequisites

- Read [decisions/0008](../../../docs/decisions/0008-youtube-search-sharepage-skeleton.md) in full (`IStreamingProvider` shape, SDK-vs-`HttpClient` rule) and [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md) in full (provider identity/trust order, cross-provider merging, failure isolation, config-optional-vs-fail-fast). Both are short enough to read before starting; this skill assumes you have.
- Have the vendor's own API docs open — the research questions below can't be answered from this repo alone.
- Know which existing provider is the closer template: `src/Hodnota.Infrastructure/Providers/YouTube/` (2 files, SDK-based) or `src/Hodnota.Infrastructure/Providers/Spotify/` (6 files: `SpotifyConfiguration.cs`, `SpotifyCredentials.cs`, `SpotifySearchDtos.cs`, `SpotifyAccessTokenProvider.cs`, `SpotifyApiClient.cs`, `SpotifyStreamingProvider.cs`) — Step 1 below decides which.

## Step 1: Research the vendor — never assume, always check

None of these have a fixed answer across providers; each one is a real investigation, not a checkbox to rubber-stamp.

1. **Official .NET SDK?** [decisions/0008](../../../docs/decisions/0008-youtube-search-sharepage-skeleton.md)'s rule: an official free SDK when the vendor publishes one (→ YouTube's path, ~2 files), a raw `HttpClient` via `IHttpClientFactory` otherwise (→ Spotify's path, ~6 files). This single answer determines most of the file count below. Qobuz, Tidal, Deezer, and Apple Music are not known to have official first-party .NET SDKs as of this writing — verify current status per vendor rather than trusting that assumption.
2. **Auth flow.** API key (YouTube's model) vs OAuth2 Client Credentials (Spotify's model) vs something else. No hodnota provider does per-user OAuth yet — if the vendor requires that instead of app-level credentials, that's new ground, flag it before implementing.
3. **Rate limits and quota.** Concrete numbers (requests/window, daily quota) differ per vendor and belong in this provider's own code/comments — but the *policy* for handling a limit hit is already fixed by [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md)'s failure-isolation section (log-and-drop-for-this-search, not block-and-retry) and doesn't need rederiving.
4. **Can valid-looking credentials silently stop working?** Spotify's gotcha — since Feb 2026, Spotify requires the app-owner's account to have an active Premium subscription just to use the Web API in Development Mode, so correct `ClientId`/`ClientSecret` alone doesn't mean search actually works. This is what justifies Spotify's config-optional DI registration instead of the fail-fast default. Check whether the new vendor has an equivalent trap (approval-gated tiers, business-verification requirements, sandbox-vs-production key splits) before deciding fail-fast vs optional in Step 3 below — **default to fail-fast unless research says otherwise.**
5. **Result/metadata shape.** Genuinely new work every time: what fields does search return, how do you map them onto `StreamingSearchResult`, does the vendor expose a release-type-like signal (Spotify's `album_type` → `Hodnota.Domain.Catalog.ReleaseType`)? This is the one step with no shortcut — read the vendor's actual search-endpoint response schema.
6. **ISRC/UPC availability.** Relevant to [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md)'s named future upgrade path (exact ISRC match once a second ISRC-bearing provider lands — Deezer, Tidal, and Qobuz are all flagged there as candidates). Note whether search results carry `isrc`/`upc`, even though nothing consumes it yet.
7. **Trust-order placement.** A judgment call, not a mechanical step: how clean/structured is this vendor's own metadata (name/artist/image) compared to the providers already in `ProviderTrustOrder.Order`? Spotify ranks above YouTube because YouTube's video titles are free text and `ChannelTitle` is often a label/VEVO channel rather than the real artist. Decide where the new provider sits based on the same kind of comparison, not roadmap order or alphabetical order.
8. **Terms of service / UI presentation.** Logo usage, required attribution, linking-back rules — flagged in [docs/roadmap.md](../../../docs/roadmap.md) as a still-open, UAT-gate-deferred item that must be checked per vendor, not assumed compliant. [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md) already committed the UI to neutral text (no logos, no brand colors) specifically so this future review can't force a redesign — keep following that until the review happens, regardless of what you find for the new vendor.
9. **Does a legitimate access path even exist?** Some vendors have no self-serve developer portal and no confirmed-working manual request process — check whether the *entire* open-source ecosystem for this vendor relies on a workaround that conflicts with the vendor's own Terms of Use (e.g. extracting credentials from a web client instead of being issued them). If so, this is a project-level decision, not a coding step: don't default into the workaround silently. Check `CLAUDE.local.md` for an existing decision on this vendor first, or raise it before writing any code — this kind of call is deliberately kept out of the public checklist above since the reasoning can be sensitive.

## Step 2: Implementation checklist

"Always" means every provider touches this file, no exceptions. "Conditional" means check first — the condition is stated, and skipping it after checking is the expected outcome for most roadmap providers, not a shortcut.

| # | File | When |
|---|---|---|
| 1 | `src/Hodnota.Application/Catalog/ProviderCodes.cs` | Always — one `public const string X = "x";` (lowercase-hyphenated). |
| 2 | `src/Hodnota.Application/Catalog/ProviderTrustOrder.cs` | Always — insert into the single `Order` array at the position decided in Step 1.7. |
| 3 | `src/Hodnota.Infrastructure/Providers/X/XStreamingProvider.cs` | Always — implements `IStreamingProvider` (`ProviderCode` + `SearchAsync`, unchanged since [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md)). |
| 3a | `.../X/XConfiguration.cs` | Always — `const string` config-key literals, [decisions/0005](../../../docs/decisions/0005-auth-identity.md)'s convention; see `SpotifyConfiguration.cs`/`YouTubeConfiguration.cs`. |
| 3b | `.../X/XCredentials.cs` | Raw-`HttpClient` path only (see `SpotifyCredentials.cs`). |
| 3c | `.../X/XSearchDtos.cs` | Raw-`HttpClient` path only — wire DTOs for the vendor's JSON (see `SpotifySearchDtos.cs`). |
| 3d | `.../X/XAccessTokenProvider.cs` | Raw-`HttpClient` path, only if the vendor needs its own token/credential caching (OAuth Client Credentials-style — see `SpotifyAccessTokenProvider.cs`). Skip for plain API-key auth. |
| 3e | `.../X/XApiClient.cs` | Raw-`HttpClient` path only (see `SpotifyApiClient.cs`). |
| 4 | `src/Hodnota.Infrastructure/DependencyInjection.cs` (`AddCatalog`) | Always — register `IStreamingProvider`, plus whatever the client mechanism needs. |
| 5 | `src/Hodnota.Api/appsettings.json` | Always — one `"X": { ... }` section, empty-string placeholders matching `XConfiguration.cs`'s keys. |
| 6 | `PlatformCodes.cs` + `Configurations/PlatformConfiguration.cs` (+ a new `AddXPlatform` migration) | Conditional — check `PlatformCodes.cs` first; Qobuz/Tidal/Deezer/AppleMusic/Bandcamp are already pre-seeded (fixed GUIDs from `InitialCreate`). Only add a migration (mirror `20260913185619_AddSpotifyPlatform.cs`'s `InsertData`/`DeleteData` pair) if the platform genuinely isn't there. |
| 7 | `web/src/api/platforms.ts` (`PLATFORM_LABELS`) | Conditional — check first; all six roadmap providers already have a label entry. |
| 8 | `tests/Hodnota.Infrastructure.Tests/Providers/X/XStreamingProviderMappingTests.cs` | Always — pure DTO→`StreamingSearchResult` mapping tests via `internal static` methods (`InternalsVisibleTo` for `Hodnota.Infrastructure.Tests` is already declared in `Hodnota.Infrastructure.csproj` — no csproj change needed). |
| 8a | `.../X/XApiClientTests.cs` | Raw-`HttpClient` path only — request shape, auth-failure retry, rate-limit handling, malformed JSON, via the shared `tests/Hodnota.Infrastructure.Tests/Providers/TestHttpMessageHandler.cs` double (reuse as-is). |
| 8b | `.../X/XAccessTokenProviderTests.cs` | Only if 3d applies (see `SpotifyAccessTokenProviderTests.cs`). |
| 9 | `tests/Hodnota.Infrastructure.Tests/DependencyInjectionTests.cs` | Only if the provider is config-optional (rare — see Step 1.4); see the two `AddInfrastructure_With/WithoutSpotifyCredentials_...` tests already there for the pattern to replicate. |
| 10 | `tests/Hodnota.Api.Tests/Catalog/CatalogApiFactory.cs` | Always — add `public StubStreamingProvider XProvider { get; } = new(ProviderCodes.X);` + one more `services.AddSingleton<IStreamingProvider>(XProvider);` line. |
| 11 | `tests/Hodnota.Application.Tests/Catalog/ProviderTrustOrderTests.cs` | Recommended, not required — add a 3-provider ordering assertion. |
| — | `SearchResultKeyTests.cs`, `SearchResultMergerTests.cs`, `CatalogSearchServiceTests.cs`, `CatalogEndpointsTests.cs` | Usually untouched — Spotify/YouTube there are stand-ins for "any two providers." Only touch if the new provider's naming exposes a genuinely new normalization case, or you're adding the first 3-provider merge test (a real gap, not required). |
| 12 | [docs/architecture.md](../../../docs/architecture.md)'s "Streaming-provider integration" section | Always — append one sentence to the existing run-on paragraph, matching its pattern: `ClassName (...), search-only via <mechanism>, <auth>. Reasoning: [decisions/NNNN](...)`. |
| 13 | [docs/roadmap.md](../../../docs/roadmap.md)'s provider checklist | Always — flip `- [ ] X` to `- [x] X`, short note, matching the Spotify line's style. |
| 14 | New ADR | Only if the provider surfaces a genuinely new decision not already covered by [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md) (e.g. the first real ISRC-based matching once a second ISRC-bearing provider lands). Not needed by default — 0011 explicitly states its patterns apply to future providers unchanged. |

## Step 3: DI registration — fail-fast by default

Steps 1 and 2 (`ProviderCodes`, `ProviderTrustOrder`) are pure mechanics; step 4's DI wiring has a real branch point. `AddCatalog` in `DependencyInjection.cs` registers YouTube unconditionally, throwing `InvalidOperationException` at startup if `YouTube:ApiKey` is missing — that's the default every provider should follow unless Step 1.4's research turned up a Spotify-style "valid credentials don't guarantee working access" gotcha. Only then does the provider become config-optional, guarding every one of its registrations (the credentials object, any named `HttpClient`s, the token provider, the API client, and the `IStreamingProvider` registration itself) behind `if (!string.IsNullOrEmpty(...))` the way Spotify's block does — see the guarded block immediately following YouTube's registration in `DependencyInjection.cs`'s `AddCatalog` method for the exact shape to copy if this applies. `CatalogSearchService` itself needs no change either way; it already iterates whatever `IStreamingProvider`s got registered.

## Verification

A provider can be fully verified before you ever get real vendor credentials, because the mapping tests and the API-level stub-based tests never call the real vendor API:

```bash
dotnet build
dotnet test tests/Hodnota.Application.Tests    # ProviderTrustOrderTests, merge/key tests
dotnet test tests/Hodnota.Infrastructure.Tests # X*MappingTests, X*ApiClientTests, X*AccessTokenProviderTests, DependencyInjectionTests
dotnet test tests/Hodnota.Api.Tests            # CatalogApiFactory/CatalogEndpointsTests via StubStreamingProvider
```

Once real credentials are available (`.env.local` / user-secrets / `appsettings.Development.json`, following [decisions/0008](../../../docs/decisions/0008-youtube-search-sharepage-skeleton.md)'s local-secret-storage precedent), do a manual smoke check via the [run-hodnota](../run-hodnota/SKILL.md) skill's agent-run path to drive a real `POST /api/catalog/search` and confirm the new provider's results actually appear, merged correctly against the existing providers.

---

## Gotchas

- **Trust-order placement is a UI-visible decision, not just an internal ranking.** [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md) ties `SharePageLink.DisplayOrder` to trust order — where you insert the new code in `ProviderTrustOrder.Order` changes the order platform links appear on every resulting share page, not just search-result-merge priority.
- **Most roadmap platforms are already pre-seeded — check before writing a migration.** `PlatformCodes.cs` and `PlatformConfiguration.cs`'s `HasData` already carry fixed-GUID seed rows for Qobuz, Tidal, Deezer, Apple Music, and Bandcamp from the original `InitialCreate` migration. Only write an `AddXPlatform` migration (mirroring `20260913185619_AddSpotifyPlatform.cs`) if the platform genuinely isn't there yet — check first, most of the time it already is.
- **`CatalogApiFactory` registers stub providers in reverse trust order on purpose.** Its own comment explains why: it's what proves `ProviderTrustOrder.Sort` is doing the sorting in endpoint tests, not registration order happening to match. Preserve that inversion when adding a third `StubStreamingProvider`.
- **Fail-fast is the default; config-optional is the exception, not a style choice.** Spotify's `if (!string.IsNullOrEmpty(...))` guard exists only because of a specific vendor fact (Premium-subscription requirement even with valid credentials) — don't copy that pattern reflexively for a new provider whose config, once set correctly, actually works.
- **A provider with no real credentials yet is not a blocked task.** Mapping tests, `TestHttpMessageHandler`-based API-client tests, and `CatalogApiFactory`'s stub-based endpoint tests cover the entire wiring without ever calling the real vendor — implement and merge the provider dormant (like Spotify currently ships), get real credentials later.

## Troubleshooting

- **`Missing required configuration value 'X:ApiKey'.`** — thrown by `AddCatalog` at startup if you followed the default fail-fast pattern (Step 3) and the config key is unset. Expected during local dev until a real value is supplied; only silence this by going config-optional if Step 1.4's research actually found a Spotify-style access gotcha.
- **The new provider's results never appear in a merged search response, with no exception anywhere.** Check that `ProviderTrustOrder.Order` actually contains the new `ProviderCodes` constant — `RankOf` returns `int.MaxValue` for anything missing from the array, which sorts it last rather than excluding it, so "forgot to add it" looks identical to "always ranked last."
- **A raw-`HttpClient` provider passes every test but produces nothing in a real run.** Check the rate-limit path specifically: [decisions/0011](../../../docs/decisions/0011-spotify-provider-and-cross-provider-result-merging.md)'s failure-isolation model turns a thrown exception into an empty result list plus one `LogWarning`, by design — a provider silently hitting its quota looks exactly like "this provider had nothing to say" unless you check the logs.
- **A new `AddXPlatform` migration's `InsertData` fails on a unique-index violation.** Means the platform code was already seeded by `InitialCreate` — go back to the Gotcha above and check `PlatformConfiguration.cs`'s `HasData` list before writing the migration at all.
