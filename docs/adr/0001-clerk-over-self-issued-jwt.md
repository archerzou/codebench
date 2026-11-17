# 1. Clerk over self-issued JWTs

Status: accepted · 2026-11-17

## Context
The template issues its own JWTs (register, login, refresh tokens, password hashing). CodeBench needs
email + GitHub sign-in, session refresh and a hosted UI, at $0.

## Decision
Use a Clerk development instance. The API validates Clerk session tokens via JWKS (`Clerk:Issuer`), with
`azp` checked against `Clerk:AuthorizedParties`. Users are mirrored locally (`User.ExternalId` = Clerk `sub`)
via the Svix-signed webhook, with an upsert on first authenticated request as a fallback. Roles and
permissions stay in **our** database; any Clerk role claim is a UI hint only.

## Consequences
- Phase 1 removes `Register`, `Login`, `Refresh`, `RefreshToken`, `TokenProvider` and `PasswordHasher`.
- Tests replace Clerk with a test JWT handler that issues `sub` + `azp`.
- Webhooks can't reach localhost; use a tunnel or dashboard test events.
