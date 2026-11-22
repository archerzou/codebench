# CodeBench — Progress Tracker

Companion to `context/build_plan.md` (rev. 2 — items are grouped under the same step numbers) and
`docs/guide/07-roadmap.md` (same phases, plus Phase 3A from guide 09). Tick each box as you complete it.
Every phase ends with a **Gate** — commands that must pass before you move on. Run
`powershell -File scripts/check-rules.ps1` **and** `powershell -File scripts/rea-audit.ps1` at every gate.

**Legend:** `[ ]` todo · `[x]` done · `[~]` in progress · `[!]` blocked (add a note in the Blockers Log)
**Tracks:** 🔍 = REA self-audit (build plan Appendix D) · 🎨 = original design (`docs/design/`)

**Status:** Phase 1 in progress — 1.0–1.3 ✅, 1.4 code ✅, 1.5 ✅, 1.6 ✅ (2026-11-21). **Open: 1.4.3** (Clerk session-token claims + webhook test — yours, not done). 1.7 ✅ (2026-11-22). 1.8 ✅ (2026-11-23). Next: 1.9 Astro site
**Started:** 2026-11-17 **Last updated:** 2026-11-23

---

## Progress Summary

| Phase | Title | Items | Done | Gate |
|---|---|---|---|---|
| 0 | Setup & self-audit baseline | 23 | 23 | Release build clean, tests green, infra `up`, RA-01..03 ok |
| 1 | Walking Skeleton | 60 | 45 | Signed in via Traefik, problem visible in own design; RA-04..07, 09 ok |
| 2 | Algorithms Grading | 57 | 0 | Algorithms graded with time + allocations; hostile suite; RA-01/03 on publish output |
| 3 | CI/CD | 20 | 1 | Merge → GHCR images; `update.sh` deploys + rolls back; `rea-audit` CI job |
| 3A | Admin Console | 28 | 0 | Create → validate → publish → learner solves; admin code lazy-only |
| 4 | Observability & Hardening | 17 | 0 | One trace API → NATS → worker; backup restored; release audit gate |
| 5 | Database Grader | 13 | 0 | EF Core problem graded on plan rules, SQL shown |
| 6 | More Graders | 11 | 0 | One problem per grader live |
| 7 | Product Polish | 10 | 0 | Progress, paths, plans, sandbox payment |
| | **Total** | **239** | **69** | |

---

## Phase 0 — Setup & self-audit baseline  ·  plan §0 · guide 01 ("own builds"), 07 §0, 08

### 0.1 Toolchain
- [x] **0.1.1** .NET SDK 10.x — `dotnet --list-sdks`
- [x] **0.1.2** Node.js 24 LTS + npm (24.18.0; REA recommends 24.22.1+ — upgrade when convenient)
- [x] **0.1.3** Docker Desktop installed (WSL2 backend, ≥ 8 GB RAM)
- [x] **0.1.4** Docker Desktop **running** — Engine 29.1.3, 16 GB (2026-11-18)
- [x] **0.1.5** `dotnet-ef` available — 10.0.11

### 0.2 Bootstrap the template
- [x] **0.2.1** Solution renamed to `CodeBench.slnx`; Todos sample removed
- [x] **0.2.2** Clean `Initial` migration; `TryDecorate` fix (KD-03); migrations `generated_code` (KD-05)
- [x] **0.2.3** `codebench` names in appsettings, compose, test factory

### 0.3 Monorepo layout
- [x] **0.3.1** `apps/`, `src/{Contracts,Worker,Sandbox.Runner}`, `sandbox-images/`, `problems/`, `deploy/`, `docs/{guide,adr}`, `scripts/`
- [x] **0.3.2** `src/Contracts` — `Subjects`, `GradeRequested` (+ `ProblemVersion`), `TestOutcome`, `GradeCompleted`, `Verdicts`
- [x] **0.3.3** `BoundaryTests` (Worker no DB, Contracts BCL-only) + `scripts/check-rules.ps1`
- [x] **0.3.4** `CLAUDE.md` extended with CodeBench invariants; `context/` docs written

### 0.4 🔍 REA self-audit setup
- [x] **0.4.1** No third-party recon — Katabench Terms reviewed, ADR 0006, `docs/recon.md`; `feature-map.md` + `api-contract.md` from own design
- [x] **0.4.2** REA CLI 6.7.0 installed; `rea doctor`: node + host ok (native engines not needed); `inspect-managed-artifact` proven on `CodeBench.Worker.dll`
- [x] **0.4.3** REA registered with Claude Code (`npx rea-agents@latest setup`), restarted, tools visible
- [x] **0.4.4** Evidence in `artifacts/rea/` (git-ignored); convention `--format json --filter-output …` (no token windows)
- [x] **0.4.5** `scripts/rea-audit.ps1` (`-Build`, `-Ci`, `-UpdateBaseline`) — RA-01 (refs + `deps.json`), RA-02, RA-03 live, RA-04..07/09 `skip`, RA-08 reference drift (info). Negative-tested: planting `Npgsql.EntityFrameworkCore.PostgreSQL` in the Worker → RA-01 FAIL via `deps.json` (DLL refs alone stayed clean)
- [x] **0.4.6** Baseline in `artifacts/rea/baseline/` + `manifest.json` (commit a95e6be, rea 6.7.0); re-run reports all refs unchanged

### 0.5 Accounts & keys
- [x] **0.5.1** Clerk dev instance: issuer, publishable key (`CLERK_PUBLISHABLE_KEY`), secret key in `deploy/.env`; dev issuer in `appsettings.Development.json` — webhook secret in 1.4.3
- [x] **0.5.2** `deploy/.env` from `.env.example`; hex secrets; `POSTGRES_PASSWORD=codebench`; `ADMIN_BOOTSTRAP_EMAIL` set

### 0.6 Version control
- [x] **0.6.1** `git init`; first commit; `deploy/.env`, `bin/`, `obj/` not tracked
- [x] **0.6.2** Public GitHub repo pushed; secret scanning + push protection on (verified via `gh api`)
- [x] **0.6.3** `docs/designs/` (third-party screenshots) git-ignored — not committed, not a design reference (ADR 0006 addendum)

> **🚦 GATE 0:**
> ```powershell
> dotnet build CodeBench.slnx -c Release      # 0 warnings
> dotnet test CodeBench.slnx                  # all green — Docker must be running
> powershell -File scripts/check-rules.ps1    # passes
> powershell -File scripts/rea-audit.ps1      # RA-01..03 ok, rest skip
> docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d
> ```
> **Last run 2026-11-18:**
> - [x] Release build — 0 warnings, 0 errors
> - [x] `dotnet test` — 23/23 (Architecture 4, Unit 12, Integration 7)
> - [x] `check-rules.ps1` — all checked rules pass (2 KD-01 baselines; SB-01/FE skipped as expected)
> - [x] Infra up: traefik (v3.6), postgres :15432 (healthy), redis :16379 (PONG), nats (JetStream), observability (healthy)
> - [x] `rea-audit.ps1` RA-01..03 ok (2026-11-19)
> - [x] REA tools visible in Claude Code
>
> ✅ **Gate 0 passed 2026-11-19.**

---

## Phase 1 — Walking Skeleton  ·  plan §1 · guide 02 §1–3,5,8,9 · 04 · 05 · 09

### 1.0 🎨 Design foundation (before 1.8; doesn't block 1.1–1.7)
- [x] **1.0.1** `docs/design/brief.md` — audience, 5 principles (numbers first, calm editor, honest feedback, keyboard first, budgets up front), voice & tone, `codebench▍` wordmark
- [x] **1.0.2** `docs/design/tokens.css` — Tailwind v4 `@theme inline` colour roles light + dark; all text pairs ≥ 5.59:1, control borders/focus ≥ 3.56:1 (measured, recorded); type, radius, shadow
- [x] **1.0.3** `docs/design/pages.md` + wireframes `catalog.md`, `editor.md` (states, behaviour, < 72 rem layouts)
- [x] **1.0.4** `docs/design/components.md` — Button, Badge, Difficulty/Track/VerdictBadge, BudgetBar (`role="meter"`), Tabs, DataTable, EmptyState, Spinner, CodePane, ThemeToggle, Kbd + Monaco theme

### 1.1 Clerk replaces template JWT (closes KD-01)
- [x] **1.1.1** Register/Login/Refresh, `RefreshToken`, `TokenProvider`, password hashing, `GetUserById`/`GetUserByEmail`, `Jwt` config, `JWT_SECRET` deleted (19 files); migration `Phase1_Clerk_Users`
- [x] **1.1.2** `AddJwtBearer` via `IOptions<ClerkOptions>`: `Authority = Clerk:Issuer`, `MapInboundClaims = false`, issuer + signature + lifetime validated (`ValidateAudience = false` justified, CA5404)
- [x] **1.1.3** `azp` checked in `OnTokenValidated` (`AuthorizedPartyValidator`: case-insensitive, trailing-slash tolerant, missing → reject)
- [x] **1.1.4** `IUserContext.ExternalId` (`sub`) + `UserId`/`TryGetUserId`, resolved once per request by `UserResolutionMiddleware`, cached `users:ext:{sub}` (misses not cached)
- [x] **1.1.5** `ClerkOptions` `ValidateOnStart`: https issuer, rejects the `YOUR-INSTANCE` placeholder, origins must be http(s) — verified the API refuses to start
- [x] **1.1.6** KD-01 baseline entries removed; `check-rules.ps1` passes with an empty baseline

### 1.2 Entities
- [x] **1.2.1** `User` — `ExternalId` (unique), `Email`, `Role`, `Plan`, `IsDisabled`, `IsDemo`, `CreatedAt`; `Create`, `CreateDemo` (`seed|…`), `ChangeRole` (idempotent, raises event), `Disable`/`Enable`; DB defaults `Learner`/`Free`
- [x] **1.2.2** `Problem` — slug unique, status/version, signature, statement, starter, reference, `Limits` (jsonb), `Tags` (`text[]`), sort order, schema/seedSql/model, plan rules/metrics (jsonb), `xmin` shadow `RowVersion`; `CreateDraft(ProblemContent, …)`
- [x] **1.2.3** `TestCase` — args/expected jsonb, `ExpectedException`, `Compare`, budgets, hidden, order; exactly-one-expectation guard + DB check; unique `(problem_id, name)`; cascade delete
- [x] **1.2.4** `RolePermission` (composite key); enums `Role`, `Plan`, `ProblemStatus`, `Track`, `Difficulty`, `GraderKind`, `CompareMode` (stored as text)
- [x] **1.2.5** `Problem.Publish()` (Draft → Published v+1, idempotent, Archived → 409) / `Archive()` (idempotent, Draft → 409); `ProblemErrors`, `UserErrors`; Created/Published/Archived + User Created/RoleChanged events
- [x] **1.2.6** 4 configurations; migration `Phase1_Problems_Users` (5 check constraints, xmin not created as a column) applied to dev DB; 26 unit + 6 Postgres integration tests (round-trip, unique slug, stale `xmin` → concurrency exception, check constraint, cascade, defaults)

### 1.3 Roles & permissions
- [x] **1.3.1** `RoleSeeder` syncs the learner/author/admin matrix (adds missing, removes stale, concurrent-safe `ON CONFLICT`); runs before requests via `RoleSeederHostedService`; clears the `permissions` cache tag on change; clear error if the schema is missing — verified on dev (20 rows) and an unmigrated Production DB
- [x] **1.3.2** `PermissionProvider(db, cache)` loads by `ExternalId` (users.role ⋈ role_permissions), cached 5 min under `permissions:{externalId}` (tagged), none for disabled users, unknown users not cached; `GET /me` now lists real permissions
- [x] **1.3.3** `Authorization/Permissions` — 11 constants + `All`; `RolePermissionMatrix` = guide 09 table (unit-tested exactly); template `Features/Users/Permissions.cs` removed

### 1.4 User sync
- [x] **1.4.1** `UserResolutionMiddleware` provisions on a miss via `UserProvisioner` (email from the custom `email` claim; learner, or admin when the email is in `Admin:BootstrapEmails` **and** `email_verified` is true; concurrent first requests → one row)
- [x] **1.4.2** `Users/HandleClerkWebhook` — `ClerkWebhookVerifier` (Svix, raw body, before parsing) in the endpoint; handler: created/updated → upsert + email sync (out-of-order safe, never changes roles), deleted → `User.MarkDeleted` (soft delete, anonymised, disabled) + cache drop; unknown types → 204; migration `Phase1_User_Deletion`
- [ ] **1.4.3** **You:** add session-token claims + webhook endpoint in the Clerk dashboard, set `CLERK_WEBHOOK_SECRET` / user-secret, send a test event (steps in build plan 1.4)

### 1.5 Seeding from `problems/`
- [x] **1.5.1** `YamlDotNet` 18.1; `YamlToJsonConverter` keeps scalar types (plain → number/bool/null, quoted → string, CRLF → LF) → `ProblemDefinition`/`TestDefinition` (unknown keys rejected) + `ProblemDefinitionValidator`; all 6 repo files unit-tested
- [x] **1.5.2** `ProblemSeeder` — insert-only (existing slug skipped, DB edits never overwritten), all files validated before any insert, one transaction per problem, race-safe, Published v1, clears the `problems` cache tag
- [x] **1.5.3** `SeedCommand`: `--seed` (role matrix + problems, then exit; 0 ok / 1 failed / 2 refused), `--seed-demo` refused outside Development (demo seeder itself = 3A.7); path from `Seeding:ProblemsPath` → `/problems` → `{repo}/problems`; verified on the host **and** in the container (`docker compose --profile app run --rm api --seed` read the `/problems` mount, re-inserted a deleted problem); unexpected failures (DB down) exit 1, not a crash
- [x] **1.5.4** Dev DB: `sum-of-evens`, `first-unique-char`, `merge-intervals`, `top-k-words` Published v1 (5/5/5/3 tests); `customers-since`, `shipping-cost-refactor` deferred automatically by grader (`ProblemSeeder.ImplementedGraders`)

### 1.6 Slices
- [x] **1.6.1** `Problems/GetProblems` — `PagedList`, track/difficulty (any case, unknown → 400), search title/slug/tag (parameterised), sort allow-list, page/pageSize clamped (≤ 100), Published only, cached `problems:list:{hash}` 10 min
- [x] **1.6.2** `Problems/GetProblem` — visible tests only + `hiddenTestCount`, no reference (type-level guard test), Pro gating (Pro plan or `admin:access`, else 404) after a caller-independent `problems:{slug}` cache
- [x] **1.6.3** `Users/GetMe` — id, email, role, plan, permissions (DB matrix), `NotProvisioned` 404
- [x] **1.6.4** `ProblemCacheKeys` — `List(hash)`, `BySlug`, `Tag`; every entry tagged; seeding clears the tag

### 1.7 Infrastructure wiring
- [x] **1.7.1** Redis as HybridCache L2 (`Common/Caching/RedisExtensions`, public `RedisCache` registered directly; dev: `localhost:16379` + password via user-secrets; unconfigured → L1-only + `/health` Unhealthy). `CachingTests`: L2 is Redis, read-through with L1 bypassed, one HybridCache instance
- [x] **1.7.2** Health checks `postgres` / `redis` / `nats` at `/health` (5 s timeout each; own `RedisHealthCheck` + `NatsHealthCheck` on NATS.Net 3.4). `HealthTests` green against Testcontainers Redis + NATS
- [x] **1.7.3** CORS: exact-origin validation in `ReadAllowedOrigins` (wildcard / path / trailing slash / no scheme fail startup; blanks dropped); unit tests + `CorsTests` preflight (4 listed origins allowed with credentials, 3 foreign denied)
- [x] **1.7.4** `--migrate` (`Database/MigrateCommand`): applies pending migrations, exits 0 (verified twice vs compose Postgres: "up to date") / 1 (verified with an unreachable DB); `--migrate --seed` chains

### 1.8 React app
- [x] **1.8.1** Vite React-TS in `apps/web`; strict tsconfig + `@/*` alias; ESLint (ts strict, react-hooks, jsx-a11y; `react-refresh` override for `components/ui/`, KD-07); `build.sourcemap: false`; deps incl. `zustand`, `lucide-react` (ADR 0007) — ESLint 9 (jsx-a11y doesn't support 10) + TypeScript 6.0 (typescript-eslint doesn't support 7) instead of create-vite's oxlint/TS 7; `strictTypeChecked`, zero warnings; Vitest + MSW 3
- [x] **1.8.2** `config.ts` (runtime `window.__CONFIG__` + `VITE_*` fallback) and `index.html` loads `/config.js` — `public/config.js` dev stub; `isDev` exported for FE-15; pre-paint theme script in `index.html`
- [x] **1.8.3** `lib/api.ts` — `useApi` (token per request), `ApiError` from ProblemDetails — also zod parse per call (FE-06), `ApiError.network`, `lib/auth.ts` `useSession()` wrapper (mocked in tests), `lib/queryClient.ts` (no retry on 4xx)
- [x] **1.8.4** 🎨 `tokens.css` wired into Tailwind (incl. shadcn bridge; `init`'s default palette deleted); `shadcn init` + `add` primitives, reviewed and re-skinned (FE-16); shared components from `components.md` with lucide icons; zustand `useThemeStore` + `useDraftStore` (FE-15); Monaco theme from tokens; light/dark — `components.json` written by hand (Radix, new-york); every primitive re-skinned (no `bg-accent` hovers, `text-white`, `bg-black`, ring halos, `next-themes`); `danger` = `bg-fail text-surface`; Clerk `@clerk/themes` shadcn theme mapped onto `--cb-*`; Monaco bundled locally (no jsDelivr), C# grammar only, lazy chunk
- [x] **1.8.5** `main.tsx` providers; routes `/`, `/problems/:slug`, `/sign-in/*`, `/sign-up/*`, `/me` — ClerkProvider inside the root route (router-aware `routerPush`/`routerReplace`); editor route `lazy()`; skip link; 404 + error pages; MissingConfig screen when the key is absent
- [x] **1.8.6** Catalog page per wireframe (TanStack Query, filters) — URL-driven filters (track/difficulty/q/sort/page), search debounced 250 ms, < 72 rem filters dialog, 6 skeleton rows, EmptyState + Clear, ProblemDetails error + Retry, pagination
- [x] **1.8.7** Editor page per wireframe — statement pane + Monaco (`csharp`) + starter code + `localStorage` drafts — budgets card, examples (param names from the signature), hidden count only, resizable + remembered split (keyboard splitter), Reset confirm (focus back to editor), drafts debounced 500 ms (zustand persist `codebench-drafts`), Run/Submit disabled with "Grading arrives in Phase 2"
- [x] **1.8.8** Dockerfile, `nginx.conf`, `docker/40-config.sh` (publishable key only) — `40-config.sh` JSON-escapes values and refuses a non-`pk_` key; CR-stripped in the image (`.gitattributes` `*.sh eol=lf`); security headers; image built + smoke-tested (config.js, SPA fallback, cache headers, no `.map`)

### 1.9 Astro site
- [ ] **1.9.1** Astro minimal + Tailwind **v4 via `@tailwindcss/vite`** (not `@astrojs/tailwind`) + sitemap + mdx, `output: 'static'`, no client framework (ADR 0007)
- [ ] **1.9.2** 🎨 Same `tokens.css` by relative import (incl. shadcn bridge); `.astro` Button/Badge/DifficultyBadge mirror the app's variants; `lucide-static` icons; dark mode via `prefers-color-scheme`; pages per `pages.md`; all copy original
- [ ] **1.9.3** Catalog generated from `problems/*/problem.yaml` (summaries only)
- [ ] **1.9.4** Pages: home, problems, problem detail, how-it-works, pricing, blog + RSS, legal
- [ ] **1.9.5** Deep links with `?entry=`; Dockerfile (additional build contexts `design` + `problems`, final image `dist/` only) + nginx

### 1.10 Compose & CI
- [ ] **1.10.1** `--profile app` builds and serves api, web, marketing through Traefik
- [ ] **1.10.2** CI frontend job enabled; Dependabot npm entries added
- [ ] **1.10.3** Hosts-file entries for `*.codebench.localhost` (PowerShell 5.1 doesn't resolve them)

### 1.11 Tests
- [x] **1.11.1** Unit: handler tests for all four Phase 1 slices (`GetProblems`, `GetProblem`, `GetMe`, `HandleClerkWebhook`) + validator tests for the one command
- [x] **1.11.2** Integration: `TestJwt` (RSA-signed `iss`/`sub`/`azp`/`exp`) through the **real** JwtBearer pipeline (static OIDC config); 7 token tests (valid, trailing slash, wrong/missing azp, wrong issuer, expired, untrusted key)
- [x] **1.11.3** Integration: raw JSON of `GET /problems/sum-of-evens` has no hidden test names, `$gen` args, reference body or the word "reference"; `hiddenTestCount: 2`
- [x] **1.11.4** Integration: anonymous catalog 200 (order, enums as strings, filters, clamp, 400s); `GET /me` 401 anonymous, 200 signed in, 404 not provisioned, miss not cached
- [ ] **1.11.5** Frontend: Vitest + MSW test for the catalog

### 1.12 🔍 REA audits
- [ ] **1.12.1** RA-04 — `Web.Api.dll` + `CodeBench.Worker.dll` ldstr literals: no secret-shaped values
- [ ] **1.12.2** RA-05 — `apps/web/dist`: no source maps, no secret literals, `pk_` only via `config.js`, routes = `routes.tsx`
- [ ] **1.12.3** RA-06 — `apps/marketing/dist`: no hidden-test values or reference solutions
- [ ] **1.12.4** RA-07 — own-app browser scenario (catalog → problem): no hidden data in any response
- [ ] **1.12.5** RA-09 — visual baselines for home, catalog, editor in `artifacts/rea/visual/`

> **🚦 GATE 1:**
> ```powershell
> docker compose -f deploy/docker-compose.yml --env-file deploy/.env --profile app up -d --build
> docker compose -f deploy/docker-compose.yml --env-file deploy/.env run --rm api --migrate
> docker compose -f deploy/docker-compose.yml --env-file deploy/.env run --rm api --seed
> ```
> - [ ] http://codebench.localhost shows the site; "Start free" opens the app
> - [ ] http://app.codebench.localhost — Clerk sign-in works and redirects back
> - [ ] A problem shows statement + starter code in the CodeBench design (light + dark)
> - [ ] http://api.codebench.localhost/health healthy
> - [ ] `dotnet test` green; `check-rules.ps1` passes with **no KD-01 baseline**
> - [ ] `rea-audit.ps1` — RA-01..07 and RA-09 ok
>
> ✅ *Self-check:* RA-07 answers "is there hidden test data in any response on the editor page?" with
> evidence — read its JSON, don't just trust the exit code.

---

## Phase 2 — Algorithms Grading Pipeline  ·  plan §2 · guide 02 §3–7 · 03 · 09

### 2.1 Entities
- [ ] **2.1.1** `Submission` with guarded, idempotent `Create`/`MarkRunning`/`MarkGraded`/`MarkFailed`
- [ ] **2.1.2** `SubmissionTestResult`
- [ ] **2.1.3** `OutboxMessage` in `Messaging/`, partial index on unprocessed rows
- [ ] **2.1.4** `UserProblemProgress` (`BestVerdict`, `Attempts`, `SolvedAt`)
- [ ] **2.1.5** Index `submissions (user_id, created_at DESC)`; migration `Phase2_Submissions_Outbox`

### 2.2 Slices
- [ ] **2.2.1** `Submissions/CreateRun` — visible tests, anonymous for free problems
- [ ] **2.2.2** `Submissions/CreateSubmission` — all tests, ≤ 2 in flight
- [ ] **2.2.3** Both: submission + outbox row in one `SaveChangesAsync`, `WithIdempotency`, 202 Accepted
- [ ] **2.2.4** Rate-limit policies `submissions` (per user) and `anonymous-runs` (per IP)
- [ ] **2.2.5** `Submissions/GetSubmission` — owner only, hidden results redacted
- [ ] **2.2.6** `Submissions/GetMySubmissions` — PagedList

### 2.3 NATS JetStream
- [ ] **2.3.1** `NATS.Net`; one `NatsClient` singleton per process
- [ ] **2.3.2** Streams `JOBS` (`jobs.>`, work-queue, 1 h) and `RESULTS` (`results.>`) created idempotently
- [ ] **2.3.3** Object store `problem-bundles` created

### 2.4 Problem bundles
- [ ] **2.4.1** `ProblemBundlePublisher` writes `{slug}/v{version}`, never overwrites
- [ ] **2.4.2** Seeder (and later Publish) writes bundles; existing problems back-filled

### 2.5 Outbox publisher
- [ ] **2.5.1** `OutboxPublisher` — `FOR UPDATE SKIP LOCKED` in one transaction, 500 ms tick
- [ ] **2.5.2** `MsgId = outbox id`; errors stored on the row; time from `IDateTimeProvider`

### 2.6 Results consumer
- [ ] **2.6.1** `GradeResultConsumer` on durable `api-results`; idempotent (already final → ack)
- [ ] **2.6.2** Writes test results, updates `UserProblemProgress`, invalidates `progress:{userId}`, then acks

### 2.7 Sandbox.Runner
- [ ] **2.7.1** Stdin reader (bundle + code), 64 KB code cap
- [ ] **2.7.2** Roslyn compile with reference allow-list; `CompileError` diagnostics with line/column
- [ ] **2.7.3** Collectible `AssemblyLoadContext`; `Solution` + method located from `Signature`
- [ ] **2.7.4** Arg binding (`System.Text.Json`) + compare modes `exact`/`unordered`/`unorderedNested`/`approx`
- [ ] **2.7.5** Generators `randomInts`/`randomStrings`/`randomIntervals`/`sortedInts` (seeded) + `$ref: reference`
- [ ] **2.7.6** Measurement: warm-up + median of 7, `GetAllocatedBytesForCurrentThread`, single thread
- [ ] **2.7.7** Nonce-delimited JSON output
- [ ] **2.7.8** Unit tests: every compare mode; each generator deterministic

### 2.8 Algorithms sandbox image
- [ ] **2.8.1** `sandbox-images/algorithms.Dockerfile` — non-root 10001, `/tmp` workdir, no problems baked in
- [ ] **2.8.2** Image loaded into `sandbox-daemon` (`docker save | … docker load`)

### 2.9 Worker
- [ ] **2.9.1** `Docker.DotNet`; `DockerClient` on `DOCKER_HOST` (never the host socket)
- [ ] **2.9.2** `SandboxOptions` (Images, Runtime, MaxParallel, Registry) bound from config
- [ ] **2.9.3** `BundleCache` reads object store, caches by key
- [ ] **2.9.4** `SandboxRunner` — `NetworkMode none`, `ReadonlyRootfs`, `/tmp` tmpfs `noexec,nosuid`, `CapDrop ALL`, `no-new-privileges`, user 10001, `PidsLimit`, `Memory == MemorySwap`, `NanoCPUs`, `Runtime`
- [ ] **2.9.5** Stdin write → start → wait with `CancelAfter` → kill → Timeout verdict
- [ ] **2.9.6** Stdout capped at 1 MB, nonce block parsed + schema-validated, else `InternalError`
- [ ] **2.9.7** Container force-removed in `finally`
- [ ] **2.9.8** `SubmissionConsumer` — durable `graders`, `MaxAckPending = MaxParallel`, `MaxDeliver = 3`, publish `GradeCompleted` (`MsgId graded-{id}`) then ack
- [ ] **2.9.9** `SandboxJanitor` — removes labelled containers older than 5 min
- [ ] **2.9.10** Images pulled at start-up (GHCR credentials for private images)

### 2.10 Hostile-submission suite
- [ ] **2.10.1** Cases: network, infinite loop, 2 GB allocation, process spawning, `/etc`+`/opt` writes, fake result JSON, correct solution — each with `expected.json`
- [ ] **2.10.2** `scripts/test-sandbox.sh` runs them with the worker's exact flags
- [ ] **2.10.3** All verdicts match

### 2.11 UI
- [ ] **2.11.1** 🎨 Results-panel wireframe in `docs/design/wireframes/`
- [ ] **2.11.2** `useSubmitCode` with fresh `Idempotency-Key`; buttons disabled while pending; Ctrl/Cmd+Enter
- [ ] **2.11.3** `useSubmissionResult` polls 750 ms until Graded/Failed
- [ ] **2.11.4** Results panel: `VerdictBadge`, name, pass/fail, µs, bytes, `BudgetBar`, `aria-live`
- [ ] **2.11.5** Compiler errors as Monaco markers
- [ ] **2.11.6** Signed-out Submit → sign-in → resubmit from `sessionStorage`

### 2.12 Tests
- [ ] **2.12.1** Testcontainers NATS (`nats:2 -js`) added to the integration factory
- [ ] **2.12.2** `POST /submissions` writes submission + outbox row; publisher emits on `jobs.submissions.created`
- [ ] **2.12.3** Fake `GradeCompleted` grades the submission and updates progress (twice = idempotent)
- [ ] **2.12.4** Third in-flight → 400 `Submissions.TooManyInFlight`; foreign submission → 404; anonymous Run on non-free → 401

### 2.13 🔍 REA audits
- [ ] **2.13.1** RA-01 full — `dotnet publish` Worker: no Npgsql/EF/Web.Api in refs or `deps.json`; config keys only `ConnectionStrings:Nats`, `Sandbox:*`, `OTEL_*`, `DOCKER_HOST`
- [ ] **2.13.2** RA-03 full — Runner DLL copied out of the algorithms image: refs ⊆ `System.*`, Contracts, `Microsoft.CodeAnalysis*`
- [ ] **2.13.3** RA-07 ext. — own-app Run scenario: hidden results expose name/passed/timings/generic message only
- [ ] **2.13.4** *(optional)* RA-10 — `rea capture-process` of the hostile suite under WSL2/CI Linux matches `expected.json`

> **🚦 GATE 2:** `--profile app --profile grading` up, image loaded:
> - [ ] Reference solution of `sum-of-evens` → Accepted with per-test µs and bytes
> - [ ] LINQ solution fails the 1 KB allocation budget; `while(true)` → Timeout
> - [ ] Hostile suite passes
> - [ ] `docker compose … exec sandbox-daemon docker ps -a` — no leftover containers
> - [ ] `dotnet test` green; `check-rules.ps1` (SB-01 active) and `rea-audit.ps1` pass
>
> ✅ *Self-check:* kill the worker mid-run, restart it — does the submission still complete (redelivery),
> and does the janitor clean the orphaned container?

---

## Phase 3 — CI/CD  ·  plan §3 · guide 06

### 3.1 ci.yml
- [ ] **3.1.1** dotnet job green on a PR (build, unit, integration via Testcontainers, architecture)
- [ ] **3.1.2** frontend matrix (web, marketing): lint, test, build
- [ ] **3.1.3** `check-rules.ps1` step

### 3.2 images.yml
- [ ] **3.2.1** api, worker, web, marketing pushed as `:<sha>` and `:latest`
- [ ] **3.2.2** Lowercase owner; GHA cache; gated on green CI

### 3.3 sandbox-images.yml
- [ ] **3.3.1** Builds on Runner/Contracts/sandbox-images changes
- [ ] **3.3.2** Runs the hostile suite before push; pushes only on `main`

### 3.4 e2e.yml
- [ ] **3.4.1** `deploy/.env.ci` committed (no secrets); `CLERK_TEST_*` and `E2E_POSTGRES_PASSWORD` as Actions secrets
- [ ] **3.4.2** Playwright smoke: free problem → Run → result row
- [ ] **3.4.3** Report + compose logs uploaded on failure

### 3.5 Repository settings
- [ ] **3.5.1** `main` protected: required checks + PR review
- [ ] **3.5.2** Dependabot for nuget, npm, docker, github-actions
- [x] **3.5.3** Secret scanning + push protection on (verified 2026-11-18)
- [ ] **3.5.4** Third-party actions pinned to versions (or SHAs)

### 3.6 Deploy & rollback
- [ ] **3.6.1** `deploy/update.sh` (+ `update.ps1`): pull → migrate → up → prune
- [ ] **3.6.2** GHCR login with `read:packages` PAT on your machine
- [ ] **3.6.3** Rollback with `update.sh <previous-sha>` tested once

### 3.7 🔍 `rea-audit` CI job
- [ ] **3.7.1** Job after publish/build (ubuntu, Node 24, pinned `rea-agents`) runs `rea-audit.ps1 -Ci`; uploads `artifacts/rea/`; fails on `FAIL`
- [ ] **3.7.2** RA-08 drift vs last `main` artifact (`compare-managed-members`, `compare-application-versions`) in the step summary
- [ ] **3.7.3** RA-09 visual diff on PRs touching `apps/**`, images attached

> **🚦 GATE 3:** PR → CI green incl. `rea-audit` artifact; merge → four images in GHCR tagged by SHA;
> `update.sh` deploys; rollback to the previous SHA works.
---

## Phase 3A — Admin Console  ·  plan §3A · guide 09

### 3A.1 Route group
- [ ] **3A.1.1** `/admin` group with `admin:access`, `Tags.Admin`; slices in `Features/Admin/`

### 3A.2 Problems
- [ ] **3A.2.1** `ListProblems`, `GetProblemForEdit` (hidden tests + reference, admin only)
- [ ] **3A.2.2** `CreateProblem` (Draft), `UpdateProblem` with `If-Match` → 409 on stale `xmin`
- [ ] **3A.2.3** Editing a Published problem edits a draft copy
- [ ] **3A.2.4** `DeleteProblem` (Drafts only), `ArchiveProblem`

### 3A.3 Tests
- [ ] **3A.3.1** `CreateTestCase`, `UpdateTestCase`, `DeleteTestCase`, `ReorderTestCases`

### 3A.4 Validate → Publish
- [ ] **3A.4.1** `ValidateProblem` runs the reference through the real worker; returns measured time/allocations
- [ ] **3A.4.2** `PublishProblem` refuses without a passing validation (409)
- [ ] **3A.4.3** Publish: `Version++`, bundle `{slug}/v{n}`, Published, `problems:*` invalidated, audited

### 3A.5 Import / export
- [ ] **3A.5.1** `ImportProblemYaml` / `ExportProblemYaml` (same shape as `problems/`)

### 3A.6 Users, submissions, audit, stats
- [ ] **3A.6.1** `ListUsers`, `ChangeUserRole` (invalidates permissions cache), `DisableUser`
- [ ] **3A.6.2** `ListAllSubmissions`, `GetAnySubmission`, `RegradeSubmission`
- [ ] **3A.6.3** `AuditLog` entity + `SaveChangesInterceptor` (Problem, TestCase, User.Role; before/after JSON)
- [ ] **3A.6.4** `ListAuditLog`; `GetAdminStats` (SQL, cached 60 s, pending jobs from consumer info)

### 3A.7 Demo data
- [ ] **3A.7.1** `DemoDataSeeder` — ana, ben, chen with realistic activity; refuses outside Development

### 3A.8 Admin UI
- [ ] **3A.8.1** 🎨 Wireframes: dashboard, problems table, problem editor (`docs/design/wireframes/admin-*`)
- [ ] **3A.8.2** Lazy `/admin` behind `RequirePermission`; Admin link from `/me`
- [ ] **3A.8.3** Dashboard (stat cards, submissions/day, verdict mix, hardest problems)
- [ ] **3A.8.4** Problems table (search, filters, status, version, actions, import)
- [ ] **3A.8.5** Problem editor tabs: Details · Statement · Code · Tests grid · Rules · Preview; Save · Validate · Publish
- [ ] **3A.8.6** Users, Submissions, Audit log pages

### 3A.9 Tests  ·  guide 09 "Tests for the admin side"
- [ ] **3A.9.1** Learner 403 on every `/admin/*`; author edits but can't publish; admin publishes
- [ ] **3A.9.2** Publish without validation → 409; stale `If-Match` → 409
- [ ] **3A.9.3** `GET /problems/{slug}` never contains hidden tests or reference (asserted on JSON)
- [ ] **3A.9.4** Publishing writes `{slug}/v{n}` to the object store (Testcontainers NATS)
- [ ] **3A.9.5** E2E: admin creates → adds test → validates → publishes; learner solves

### 3A.10 🔍 REA audits
- [ ] **3A.10.1** RA-05 ext. — admin code only in lazy chunks; learner entry chunk has no `/admin` routes
- [ ] **3A.10.2** RA-07 ext. — learner scenario never requests `/admin/*`; hidden data only on admin routes

> **🚦 GATE 3A:** all 3A.9 tests green; 3A.10 ok; a new problem goes live without rebuilding any image; an
> in-flight submission still grades against the version it was submitted for.

---

## Phase 4 — Observability & Hardening  ·  plan §4 · guide 03 checklist · 05 · 07 §4

### 4.1 Tracing
- [ ] **4.1.1** `traceparent` in NATS headers on publish (outbox, worker); restored on consume
- [ ] **4.1.2** One trace spans API → NATS → worker → result in Tempo

### 4.2 Dashboards & alerts
- [ ] **4.2.1** Submissions/min, grading latency p50/p95, verdict mix
- [ ] **4.2.2** `JOBS` queue depth, sandbox failures
- [ ] **4.2.3** DinD volume disk usage alert
- [ ] **4.2.4** Decision recorded: keep `otel-lgtm` or split services (KD-04)

### 4.3 Sandbox hardening checklist (guide 03)
- [ ] **4.3.1** All container flags verified (SB-01); timeout enforced by worker + `finally` remove (SB-03)
- [ ] **4.3.2** Output cap + nonce + schema validation (SB-04)
- [ ] **4.3.3** Worker image: no Postgres client libs, no connection strings (RA-01 on the image)
- [ ] **4.3.4** Host socket mounted nowhere but Traefik
- [ ] **4.3.5** Per-user in-flight limit (API) + `MaxAckPending` (NATS) confirmed
- [ ] **4.3.6** gVisor (`runsc`) tried on Linux/WSL2 native engine; timings compared
- [ ] **4.3.7** Redis and NATS passwords enforced

### 4.4 🔍 Release audit gate
- [ ] **4.4.1** `rea-audit.ps1 -FromImages <sha>` runs every RA on files extracted from the GHCR images; failure blocks `update.sh` for that SHA
- [ ] **4.4.2** RA-08 diff between previous and new release SHA recorded in the release notes

### 4.5 Backups
- [ ] **4.5.1** `--profile backup` writes `./backups/*.sql.gz`
- [ ] **4.5.2** A backup restored into a scratch database end to end

> **🚦 GATE 4:** a Run produces one connected trace; dashboards populated; restored backup contains your
> problems; hardening checklist fully ticked; latest release has its audit attached.

---

## Phase 5 — Database / EF Core Grader  ·  plan §5 · guide 03 · 09 problem 5

- [ ] **5.1.1** `database.Dockerfile` + `db-entrypoint.sh`; Postgres in `/tmp/pgdata`, Unix socket only
- [ ] **5.1.2** Pre-initialised data dir baked in; tmpfs ~256 MB for database images
- [ ] **5.2.1** Harness runs bundle `schema` + `seedSql` + `ANALYZE`; builds `DbContext` from `model`
- [ ] **5.2.2** `DbCommandInterceptor` captures SQL + parameters; results compared
- [ ] **5.2.3** `EXPLAIN (FORMAT JSON)` per captured statement
- [ ] **5.3.1** Plan rules `noSeqScan`, `usesIndex`, `maxRoundTrips`; outcomes in `Details`
- [ ] **5.4.1** 🎨 UI: expandable SQL + plan summary + rule outcomes per test (wireframe first)
- [ ] **5.5.1** `customers-since` seeded; plan rule checked against the reference on seeded data
- [ ] **5.5.2** Original problem: N+1
- [ ] **5.5.3** Original problem: filter before fetch
- [ ] **5.5.4** Original problem: sargable date range
- [ ] **5.5.5** Original problems: projection, pagination
- [ ] **5.6.1** 🔍 RA-03 allow-list for the database image (+ EF Core, Npgsql) — other images unchanged

> **🚦 GATE 5:** `customers-since` reference → Accepted; in-memory filtering and N+1 both fail `one-trip`;
> SQL and plan visible in the UI; RA-03 ok for both images.

---

## Phase 6 — More Graders  ·  plan §6 · guide 03 "Other graders"

- [ ] **6.1.1** Refactoring: Roslyn metrics (complexity, nesting, method length, duplication) vs thresholds
- [ ] **6.1.2** `shipping-cost-refactor` seeded; starter fails metrics, refactor passes
- [ ] **6.2.1** Architecture: multi-file submissions compiled into separate assemblies
- [ ] **6.2.2** NetArchTest rules + one original problem
- [ ] **6.3.1** Secure coding: adversarial suites (path traversal, SQL injection)
- [ ] **6.3.2** One original secure-coding problem
- [ ] **6.4.1** Test writing: user tests vs correct implementation + planted mutants
- [ ] **6.4.2** One original test-writing problem
- [ ] **6.5.1** Hostile-suite cases added for each new image/grader
- [ ] **6.5.2** 🎨 UI renders each grader's `Details` (shared components)
- [ ] **6.5.3** 🔍 RA-03 allow-list entry per new grader image

> **🚦 GATE 6:** one problem per grader: reference → Accepted, known-bad → rejected for the right reason.

---

## Phase 7 — Product Polish  ·  plan §7 · guide 07 §7

- [ ] **7.1.1** `Progress/GetProgress` — solved, attempts, streaks, XP; cached `progress:{userId}`
- [ ] **7.1.2** 🎨 Catalog checkmarks + progress page (designed in `docs/design/` first)
- [ ] **7.2.1** Learning paths: ordered lists with prerequisites (entity + admin CRUD)
- [ ] **7.2.2** Learner view of paths
- [ ] **7.3.1** Free vs Pro tracks mapped to permissions; `GetProblem` enforces
- [ ] **7.4.1** Payment provider chosen (Paddle **or** Stripe) — ADR written
- [ ] **7.4.2** Sandbox checkout from the web app (public token only)
- [ ] **7.4.3** Signed payment webhook slice changes `User.Plan`
- [ ] **7.4.4** 🔍 RA-05 ext. — only the public payment token in the bundle
- [ ] **7.5.1** Optional: Cloudflare Tunnel + Clerk production instance on own domain

> **🚦 GATE 7:** a sandbox payment flips a user to Pro and unlocks a Pro track; progress and streaks render.

---

## Standing checks (every phase)  ·  guide 08 "Handling rules"

- [x] `.gitignore` covers `deploy/.env`, `**/.env.local`, `apps/marketing/.env`, `backups/`, `captures/`, `artifacts/`, `docs/designs/`
- [x] Only `.env.example` files committed, secrets empty
- [ ] Publishable keys only in the browser — verified by RA-05 before releases
- [ ] Worker receives no database, Clerk or payment secrets — verified by RA-01
- [ ] Test-mode keys only; live keys never leave the production machine
- [ ] A leaked key is rotated immediately and replaced everywhere
- [ ] No third-party product content (screenshots, copy, assets) in the repo or used as a reference (ADR 0006)
- [ ] REA is pointed only at CodeBench artefacts or `*.codebench.localhost`

---

## Blockers Log

| Date | Phase | Problem | Resolution |
|---|---|---|---|
| 2026-11-17 | 0 | Docker Desktop not running → integration tests not run | ✅ 2026-11-18 Docker running, `dotnet test` green |
| 2026-11-18 | 0 | `compose up`: redis can't bind 127.0.0.1:6379 (Windows WinNAT excluded range 6373–6472); host 5432 already used by a native Postgres | ✅ Dev override maps 15432 / 16379; `appsettings.Development.json` + guide 05/08 updated |
| 2026-11-18 | 0 | Traefik v3.3 docker provider fails (`Error response from daemon: ""`) — requests API 1.24, Docker Engine 29 requires ≥ 1.44 | ✅ `traefik:v3.6` in compose + guide 05 |
| 2026-11-18 | 0 | PowerShell 5.1 can't resolve `*.codebench.localhost` | Use `curl.exe -H "Host: …" http://127.0.0.1` or hosts-file entries (1.10.3) |
| 2026-11-19 | 0 | REA rejects `--token-limit` with structured output; only lists direct assembly refs | Use `--format json --filter-output`; RA-01 also scans `deps.json` |
| 2026-11-22 | 1 | Host-run API: `/health` nats Unhealthy (`can not connect uris`) with `nats://…@localhost:4222` | ✅ `localhost` → `::1` first, ports bound to 127.0.0.1, NATS.Net tries only the first address; user-secret uses `127.0.0.1`; guide 08 updated. `/health` all Healthy |
| 2026-11-22 | 1 | Docker Desktop stopped mid-session → final integration run for 1.7 not done (86 tests fail at container start, not in code) | ✅ 2026-11-22 Docker restarted; full suite green 3× in a row (296 tests) |

## Decisions Log

Record every deviation from `build_plan.md` / the guide and why — future-you will ask.

| Date | Decision | Rationale |
|---|---|---|
| 2026-11-17 | Keep `src/Web.Api` folder + `Web.Api` namespace | Guide keeps it; less churn |
| 2026-11-17 | `TryDecorate` instead of `Decorate` (KD-03) | `Decorate` throws when a handler shape has no implementations |
| 2026-11-17 | Migrations marked `generated_code` (KD-05) | Tool output fails IDE0161 under analyzer-strict |
| 2026-11-17 | Compose profiles `app` / `grading` / `backup` | `up` works while the project is built phase by phase |
| 2026-11-17 | Single `otel-lgtm` container (KD-04) | Template's stack; split later if needed |
| 2026-11-17 | Guide 09 wins over 00/02/03: DB source of truth, bundles via NATS (ADR 0005) | Admin edits without image rebuilds |
| 2026-11-18 | YAML lives in `problems/` (not `seed/problems/`); seed data only | User preference; DB owns content after seeding |
| 2026-11-18 | `IDateTimeProvider` (template) instead of guide's `TimeProvider` (KD-02) | One time abstraction across the codebase |
| 2026-11-18 | Webhook logic in a handler, not the endpoint (EP-03) | Guide 02 snippet is illustrative; keeps endpoints thin |
| 2026-11-18 | Dev host ports Postgres **15432**, Redis **16379** (container ports unchanged) | Avoid native Postgres on 5432 and Windows' reserved 6373–6472 range |
| 2026-11-18 | `traefik:v3.6` instead of `v3.3` | Docker Engine 29 dropped API < 1.44 |
| 2026-11-18 | **No Katabench recon** (ADR 0006); feature map + API contract from our own design | Their Terms forbid using the Service to build a competing product, reverse-engineering and scripted API scraping |
| 2026-11-18 | `deploy/.env` uses `CLERK_PUBLISHABLE_KEY`; `VITE_CLERK_PUBLISHABLE_KEY` only in `apps/web/.env.local` | Compose reads the former |
| 2026-11-18 | Generated secrets are hex, not base64 | base64 `/`,`+`,`=` break `nats://user:pass@` and Redis connection strings |
| 2026-11-19 | Build plan rev. 2: REA self-audit track (RA-01..09, `scripts/rea-audit.ps1`) in every phase | Turns boundary/secret/hidden-data rules into evidence on the shipped artefacts, not just source checks |
| 2026-11-19 | Original design track (`docs/design/`, §1.0); third-party screenshots git-ignored and not used (ADR 0006 addendum) | Same ToS issue as recon; avoids reproducing another product's look; repo is public |
| 2026-11-19 | Phase 0 renamed "Setup & self-audit baseline"; recon items replaced by REA setup items | Recon dropped; REA's value is on our own builds |
| 2026-11-19 | `rea-audit.ps1`: missing artefact = `skip` locally, `FAIL` with `-Ci`; RA-08 is informational | Script runs in every phase without false failures, but CI can't silently skip a boundary check |
| 2026-11-19 | REA baseline lives in git-ignored `artifacts/rea/baseline/` locally; CI uses the last `main` run's artifact | Evidence contains absolute local paths; not worth committing |
| 2026-11-19 | `User` reshaped during 1.1 (migration `Phase1_Clerk_Users`); Role/Plan/flags follow in 1.2's `Phase1_Problems_Users` | Removing password hashing and keying users by Clerk `sub` can't wait; keeps 1.2 focused on the role matrix |
| 2026-11-19 | `UserId` resolved by `UserResolutionMiddleware` (after authentication) instead of lazily inside `UserContext` | A lazy DB lookup in a sync property would be sync-over-async (XC-03); 1.4.1 turns it into the upsert |
| 2026-11-19 | `PermissionAuthorizationHandler` fails closed; template "succeed for any authenticated user" TODO removed | SC-03; until 1.3 no permission is granted, so no `HasPermission` endpoint is open by accident |
| 2026-11-19 | Template `GetUserById` / `GetUserByEmail` deleted; minimal `GetMe` added early | They exposed first/last name (gone) by id; `GetMe` gives the auth tests a real protected route |
| 2026-11-19 | Integration tests keep the real JwtBearer handler and swap only the key source (`StaticConfigurationManager`) | A fake auth scheme would skip issuer/lifetime/azp validation — exactly what SC-02 needs tested |
| 2026-11-19 | `ValidateAudience = false` with a justified `#pragma` (CA5404) | Clerk session tokens have no `aud`; `azp` check replaces it |
| 2026-11-19 | Naming rules NM-01…NM-14 (`code_rules.md` Part 12): 10 script checks (case-sensitive) + `.editorconfig` IDE1006 as build error | Consistency enforced by tooling, not memory; each check probe-tested to fail on a planted violation |
| 2026-11-19 | `context/` keeps snake_case file names (exception to NM-12) | Working docs referenced by name in prompts; user decision |
| 2026-11-19 | CQRS contract method stays `Handle` (exception to NM-04) | Template/guide/skills convention; user decision — no churn |
| 2026-11-19 | Tests: one class per use case (`{UseCase}HandlerTests` / `ValidatorTests` / `{UseCase}Tests`), skills updated | Mirrors one-file-per-slice (VS-01); skills had 3 conflicting patterns |
| 2026-11-19 | `Contracts/Grading.cs` split into one file per record; middleware `Invoke` → `InvokeAsync` | NM-01 / NM-05; the rename exposed a real bug: `RequestContextLoggingMiddleware` dropped `CorrelationId` after the first await (now awaited inside `using`) |
| 2026-11-20 | `Problem.CreateDraft(ProblemContent, …)` / `TestCase.Create(…, TestCaseContent)` content records | One shape for seeder (1.5), create/update (3A); avoids 15-parameter factories (Sonar S107) |
| 2026-11-20 | `xmin` mapped as shadow property `RowVersion`; `Problem.Version` = publish counter (0 while Draft, ≥ 1 once Published — DB check) | Keeps the entity persistence-ignorant (DM-05) and the two "versions" from being confused |
| 2026-11-20 | No `TestCase.Generator` column (guide 09 table lists one) | Guide 09's own test format keeps generators inside `args` as `{"$gen": …}`; a column would duplicate it |
| 2026-11-20 | All enums stored as text; `Track`/`GraderKind` already list Phase 6 kinds | Readable SQL, reorder-safe; avoids a migration per grader |
| 2026-11-20 | `Archive()` refuses Drafts (409), `Publish()` refuses Archived (409); both idempotent | Guide 09: drafts are deleted, not archived; republishing goes through a new draft (DM-08) |
| 2026-11-20 | `ApplicationDbContext` now passes the `CancellationToken` to domain-event dispatch | VS-06; template dropped it |
| 2026-11-20 | One permission catalog `Authorization/Permissions` (+ `RolePermissionMatrix`), not per-feature `Permissions` classes | The matrix spans every area; per-feature classes with the same name would clash; AR-07 puts permission loading in `Authorization/` (AR-08, NM-06 updated) |
| 2026-11-20 | `RoleSeeder` syncs (adds + removes) rather than insert-only; runs in a hosted service at start-up | The matrix in code is the source of truth (renamed permissions must disappear); requests never see an empty table; two instances can't collide (`ON CONFLICT`) |
| 2026-11-20 | Permission cache entries tagged `permissions`; seeder clears the tag on change | Per-user keys can't be enumerated; a matrix change affects everyone |
| 2026-11-20 | First-request email comes from custom session-token claims (`email`, `email_verified`), not Clerk's Backend API | Documented Clerk mechanism; no outbound call per new user, no secret key needed in host-run dev; one-time dashboard step |
| 2026-11-20 | Bootstrap admin needs a **verified** email (token `email_verified` true, or webhook `verification.status = verified`); only at creation | Fail closed: a missing claim or unverified address can never mint an admin; existing users are never promoted by config |
| 2026-11-20 | `user.deleted` → soft delete (`DeletedAt`, disabled, email anonymised, row kept); `Enable()` refuses deleted users (409) | Submission/audit history stays intact; an admin can't resurrect a Clerk-deleted account |
| 2026-11-20 | Webhook: 503 when `Clerk:WebhookSecret` is unset, 400 on bad signature/payload, 204 for unknown types | Never processes unverified data; unconfigured ≠ attack; Svix retries 4xx/5xx, so ignored types must still 2xx |
| 2026-11-20 | YAML → JSON tree (scalar style decides type) → System.Text.Json, instead of YamlDotNet's object deserializer | YamlDotNet turns every scalar into a string (`expected: 6` → `"6"`); quoted vs plain must survive into `args_json`/`expected_json` |
| 2026-11-20 | Scalar line breaks normalised CRLF → LF | Seed files have CRLF on Windows (259 CR bytes found); starter/reference code must not depend on the machine that seeded |
| 2026-11-20 | Seed deferral by grader (`ProblemSeeder.ImplementedGraders = {Algorithms}`) instead of per-file flags | Problems 5–6 seed themselves once their grader exists; no YAML edits needed later |
| 2026-11-20 | `reference` optional only for the refactoring grader (falls back to the starter) | `shipping-cost-refactor`'s starter is its behaviour reference (guide 09) |
| 2026-11-20 | `sortOrder` added to the six seed files (10…60) | Catalog order = guide 09 numbering; YAML had none |
| 2026-11-20 | All seed files validated before any insert; one transaction per problem | A broken file must not leave a half-seeded catalog; problem + tests are atomic |
| 2026-11-21 | Enums serialised as camelCase strings globally (`JsonStringEnumConverter`) | `"algorithms"`, `"learner"` instead of ordinals; matches guide 09 role names and NM-08 |
| 2026-11-21 | `GET /problems` binds `track`/`difficulty` as strings and parses case-insensitively; unknown → 400 `Problems.InvalidFilter` | Built-in enum binding is case-sensitive and rejected the API's own camelCase values |
| 2026-11-21 | Pro (non-free) problems: listed in the catalog, opened only by `plan = pro` or `admin:access` (staff preview); others get 404 | EP-02 don't-leak-existence; staff must preview what they manage; Phase 7.3 swaps the plan check for plan-derived permissions |
| 2026-11-21 | `problems:{slug}` caches caller-independent data; the Pro check runs per request after the cache | One cache entry per problem, never a per-user copy, and no way for a cached entry to grant access |
| 2026-11-21 | `ProblemLimits.Default` returns a new instance; `Problem.Apply` copies limits | **Bug from 1.2:** a shared static owned-type instance made saving two default-limit problems in one context throw (would hit `--seed` and admin create) |
| 2026-11-21 | `GlobalExceptionHandler`: `BadHttpRequestException` → its own status (400) and every error as `application/problem+json` | Unbindable query values (`?page=abc`) returned 500; responses weren't RFC 7807 media type |
| 2026-11-21 | `check-rules.ps1` NM-07 ignores comment lines | It read `<see cref="Tag"/>` in a doc comment as a cache key; probe-tested that real bad keys still fail |
| 2026-11-21 | `.dockerignore` line `README.md**/.env.*` split in two | A missing newline merged two patterns, so `**/.env.*` (e.g. `.env.local`) wasn't excluded from image build contexts; built image verified to contain no env files |
| 2026-11-21 | `SeedCommand` maps every unexpected failure to exit 1 (justified CA1031 at the CLI boundary) | DB unreachable previously crashed with exit 139; scripts and Compose need the documented 0/1/2 |
| 2026-11-20 | Base `appsettings.json` gets a Serilog Console sink | Production had **no** logging configured (only Development did): containers and `--seed` were silent. OTLP logs in Production remain Phase 4 |
| 2026-11-20 | Signed-in work endpoints use `HasPermission(...)` instead of bare `RequireAuthorization()` | `IsDisabled` is enforced in the permission handler (guide 09); bare authorization would let disabled users submit |
| 2026-11-23 | Astro: Tailwind v4 via `@tailwindcss/vite` (guide 04 had `@astrojs/tailwind`, v3-only); no React islands; `.astro` components mirror the app's variants; `lucide-static`; check-rules FE-11/FE-13 for `apps/marketing` (probe-tested) | v3 integration can't read `tokens.css` (`@theme`, `@custom-variant`); one design system across both sites without shipping React to the marketing site |
| 2026-11-23 | Web + marketing Docker builds get `docs/design` (+ `problems`) as **additional build contexts** at the same relative paths; compose + guide 06 `images.yml` updated | Build contexts were `apps/<app>` while `tokens.css` and `problems/*.yaml` live outside — `docker build` would have failed; root context is blocked by `.dockerignore` (`apps`, `docs`) and would bloat the context |
| 2026-11-22 | Frontend adds shadcn/ui + lucide-react + zustand (ADR 0007); rules FE-15/16/17, KD-07; shadcn bridge in `tokens.css`; check-rules FE-11/15/17 (probe-tested) | Accessible Radix primitives instead of hand-rolled dialogs/menus; zustand only for client UI state. No REA conflict — RA-05 additionally rejects devtools hooks in `dist/` |
| 2026-11-22 | Register the public `RedisCache` as `IDistributedCache` instead of `AddStackExchangeRedisCache()`; health check has its own lazy connection | **Bug found by `ProblemSeederTests`:** `RedisCacheImpl` calls `GetService<HybridCache>()` on connect (client-name suffix only), and HybridCache's ctor connects to read tag state → re-entrant DI → **two HybridCache singletons** (always with `ConnectionMultiplexerFactory`, timing-dependent otherwise; reproduced in a bare `ServiceCollection`, stack-traced). Middleware (method injection) and handlers (ctor injection) got different instances, so invalidations missed → stale data up to 10 min. Pinned by `CachingTests` |
| 2026-11-22 | Own Redis/NATS health checks instead of `AspNetCore.HealthChecks.Redis/Nats`; NATS.Net 3.4.1 | The NATS package pins NATS.Net 2.x; own checks report a missing connection string as Unhealthy instead of throwing |
| 2026-11-22 | Redis/NATS removed from `appsettings.Development.json`; user-secrets only | Compose Redis/NATS require passwords, so the password-less dev values could never authenticate |
| 2026-11-22 | Integration tests run real Redis + NATS (Testcontainers) | `/health` and the L2 path behave as in production; caught the double-HybridCache bug |
| 2026-11-23 | `apps/web` lint/type stack: ESLint 9 + typescript-eslint `strictTypeChecked` + TypeScript **6.0** (create-vite 9 now ships oxlint + TS 7) | FE-12 names ESLint + jsx-a11y; `eslint-plugin-jsx-a11y` supports ESLint ≤ 9 and `typescript-eslint` TS < 6.1. Revisit when both catch up |
| 2026-11-23 | Monaco bundled from `monaco-editor` (`loader.config({ monaco })`, editor worker via `?worker`, only the C# grammar) instead of `@monaco-editor/react`'s jsDelivr default; `dompurify` pinned ≥ 3.4.18 via `overrides` | No third-party script at runtime (CSP/offline, RA-05 evidence stays local); Monaco 0.57 depends on a DOMPurify with open advisories |
| 2026-11-23 | `components.json` written by hand; shadcn CLI used only for `add` | The current CLI's `init` is preset-based (no `new-york`/Radix path non-interactively); without `lib/utils.ts` the CLI resolved `cn` to an unrelated npm package named `cn` — removed, `lib/utils.ts` added, imports fixed |
| 2026-11-23 | ClerkProvider inside the root route instead of around `RouterProvider` (guide 04 snippet) | Clerk gets `routerPush`/`routerReplace`, so sign-in steps navigate client-side; QueryClientProvider stays in `main.tsx` |
| 2026-11-23 | Clerk UI themed with `@clerk/themes` shadcn theme; unprefixed `--primary`, `--muted`, … mapped onto `--cb-*` in `apps/web/src/index.css` | Clerk's theme reads unprefixed shadcn vars, which `tokens.css` only exposes via `@theme inline`; same pairs as the bridge, so the measured contrast holds |
| 2026-11-23 | App shell in `src/app/` (RootLayout, AppLayout, AppHeader, NotFound, RouteError, MissingConfig); `Track`/`Difficulty` + `trackLabel` in `lib/domain.ts` | FE-02's tree has no home for layout; shared enum types can't live in component files (react-refresh) or a feature (components may not import features) |
| 2026-11-23 | Drafts persist under one zustand key `codebench-drafts` (slug → code), not `draft:{slug}` per key as the editor wireframe says | FE-15 / guide 04: `persist` with explicit `name` + `version`; one store, one migration path |
| 2026-11-23 | Catalog rows show title · track · difficulty · Free/Pro · tags; **no statement summary or budgets** (wireframe shows both) | `ProblemSummary` has neither field; adding them is an API-contract change (`summary`, budget hints) — left for a later slice rather than faked client-side |
| 2026-11-23 | Header has no Admin link yet | `/admin` arrives in 3A; a link to a 404 is worse than none |
| 2026-11-19 | `check-rules.ps1` XC-02 rewritten | It was vacuous: `Find` skips `#` lines and `tests/` wasn't scanned — an unjustified pragma in `BaseHandlerTest` had slipped through |

---

## Quick Health Check

```powershell
dotnet build CodeBench.slnx -c Release                    # compiles, 0 warnings?
dotnet test CodeBench.slnx                                # green? (Docker running)
powershell -File scripts/check-rules.ps1                  # source rulebook?
powershell -File scripts/rea-audit.ps1                    # artefact rulebook (REA)?
dotnet list package --vulnerable --include-transitive     # audit debt?
dotnet list package --outdated                            # drift?
docker compose -f deploy/docker-compose.yml --env-file deploy/.env ps   # stack healthy?
```

Then walk the PR checklist in `context/code_rules.md`.
