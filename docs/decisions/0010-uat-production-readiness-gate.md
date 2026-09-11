# 0010. UAT / production-readiness gate

Status: accepted

## Context

This is a solo, pre-1.0 project that intends to eventually open to real users — the manifesto's goal is to "start sharing music with people one day, and once other people like it," have them use it too ([MANIFESTO.md](../MANIFESTO.md)). [workflow.md](../workflow.md) already defines `main` (always deployable) and `develop` (integration/alpha), but nothing between "integrated on `develop`" and "real strangers can register" — there is no defined checkpoint for "is this actually ready to let people sign up."

An SDLC (Software Development Life Cycle) documentation audit found several related concerns already exist, but only as scattered mentions rather than a real gate:

- [architecture.md](../architecture.md)'s Hosting section mentions disaster-recovery and security posture in a single aspirational sentence, with no roadmap item behind it.
- Automated security scanning is wired up (CodeQL, Dependabot, secret scanning, CycloneDX SBOM — [decisions/0008](0008-youtube-search-sharepage-skeleton.md)), but nothing catches logic-level security mistakes a human reviewer would (e.g. an endpoint that lets one user reach another user's data).
- Nothing documents what happens to a real user's stored data (email, password hash, via ASP.NET Core Identity) once real accounts exist.
- [roadmap.md](../roadmap.md) has no observability/monitoring item at all — if a deployed instance crashed, nothing would notice.

## Decision

Introduce **UAT** (User Acceptance Testing — a trial deployment before full public release) as a milestone between `develop`'s integration/alpha state and full public availability: a deployment on the real, eventually-chosen hosting target, with registration limited to the author and a small invited group rather than the general public.

**Crossing from UAT to full public production requires all of the following to be true** — a checklist, not a vague aspiration:

1. Hosting finalized (existing roadmap item).
2. Backup & disaster recovery plan defined, and its restore path actually tested — promoted from the one-sentence mention in `architecture.md`'s Hosting section into real, verified work.
3. Observability in place: structured logging conventions plus basic error/crash alerting, so a production failure is actually noticed rather than discovered by a user complaint.
4. License decided and a `LICENSE` file committed (existing roadmap item) — "all rights reserved by default" is an acceptable placeholder pre-release, not once real users depend on the project.
5. Privacy policy published, describing what personal data (email, password hash) is collected, why, and how long it is kept.
6. Human security review completed — a deliberate, manual pass distinct from CI's automated scanners. At minimum: authorization logic (can user A reach user B's private data), CSP (Content Security Policy — a browser response header restricting which script/style/image sources a page may load, a defense against XSS), other secure response headers (e.g. HSTS), and secure cookie attributes (`HttpOnly`/`Secure`/`SameSite`) if auth tokens ever move off `localStorage`.

Real email delivery (roadmap items "Integrate email service" and "Use email service for auth confirmation flows") is a practical prerequisite for UAT itself, not just for full production: Identity's `RequireConfirmedAccount = true` means an invited UAT tester cannot confirm an account without it — `NoOpEmailSender` only logs the confirmation link today ([decisions/0005](0005-auth-identity.md), [decisions/0009](0009-auth-ui-search-share-web.md)).

**Explicitly not required to cross this gate** — deferred on purpose:

- Third-party streaming-API terms-of-service review (YouTube, Qobuz, Tidal, Deezer, Apple Music, Bandcamp) — acceptable to review after UAT, since this is not a commercial product.
- Accessibility (WCAG) conformance.
- A formal legal-correctness pass on the privacy policy (e.g. a lawyer/template review) — having a privacy policy is required above; verifying its legal correctness is not, at this stage.
- A cookie-consent banner — not applicable while the app sets no cookies at all (auth tokens live in `localStorage`, per `architecture.md`'s Authentication section); revisit only if that changes.

This ADR does not decide the concrete tooling for monitoring, backups, or the security-review method (e.g. which logging library, which backup tool, an internal checklist vs. an external pentest) — those are implementation-time decisions, made once hosting is chosen and the gate is actually being approached, consistent with this project's established YAGNI discipline (e.g. [decisions/0007](0007-catalog-data-model.md)'s JSONB/metadata-column rejection for the same reasoning pattern).

## Consequences

- [roadmap.md](../roadmap.md) gains new items reflecting this checklist (observability, backup & disaster recovery, this gate itself, and a post-gate third-party ToS review item), sequenced after hosting/license/email and before "Implement auth (Google/Facebook external login)."
- [architecture.md](../architecture.md)'s Hosting section is updated to link forward to this ADR and the new roadmap items instead of carrying the disaster-recovery/security mention as an isolated sentence.
- Future features that touch user data or auth should be checked against this gate's checklist before a UAT/production release is called "done" — not just against their own feature-level tests.
- If real user feedback during UAT surfaces a need this checklist didn't anticipate, this ADR gets revisited or superseded, not silently worked around.
