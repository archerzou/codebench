# CodeBench — Progress Tracker

Companion to `context/build_plan.md` (rev. 2 — items are grouped under the same step numbers) and
`docs/guide/07-roadmap.md` (same phases, plus Phase 3A from guide 09). Tick each box as you complete it.
Every phase ends with a **Gate** — commands that must pass before you move on. Run
`powershell -File scripts/check-rules.ps1` **and** `powershell -File scripts/rea-audit.ps1` at every gate.

**Legend:** `[ ]` todo · `[x]` done · `[~]` in progress · `[!]` blocked (add a note in the Blockers Log)
**Tracks:** 🔍 = REA self-audit (build plan Appendix D) · 🎨 = original design (`docs/design/`)

**Status:** Phase 0 ✅ complete (2026-11-19) — next: Phase 1 (1.0 design and 1.1 Clerk can run in parallel)
**Started:** 2026-11-17 **Last updated:** 2026-11-19

---

## Progress Summary

| Phase | Title | Items | Done | Gate |
|---|---|---|---|---|
| 0 | Setup & self-audit baseline | 23 | 23 | Release build clean, tests green, infra `up`, RA-01..03 ok |
| 1 | Walking Skeleton | 60 | 0 | Signed in via Traefik, problem visible in own design; RA-04..07, 09 ok |
| 2 | Algorithms Grading | 57 | 0 | Algorithms graded with time + allocations; hostile suite; RA-01/03 on publish output |
| 3 | CI/CD | 20 | 1 | Merge → GHCR images; `update.sh` deploys + rolls back; `rea-audit` CI job |
| 3A | Admin Console | 28 | 0 | Create → validate → publish → learner solves; admin code lazy-only |
| 4 | Observability & Hardening | 17 | 0 | One trace API → NATS → worker; backup restored; release audit gate |
| 5 | Database Grader | 13 | 0 | EF Core problem graded on plan rules, SQL shown |
| 6 | More Graders | 11 | 0 | One problem per grader live |
| 7 | Product Polish | 10 | 0 | Progress, paths, plans, sandbox payment |
| | **Total** | **239** | **24** | |

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
- [ ] **1.0.1** `docs/design/brief.md` — audience, 3–5 principles, voice & tone, name/wordmark direction (original)
- [ ] **1.0.2** `docs/design/tokens.css` — Tailwind v4 `@theme`: colour roles light + dark (AA ratios recorded), type, spacing, radius
- [ ] **1.0.3** `docs/design/pages.md` + low-fi wireframes for catalog and editor
- [ ] **1.0.4** `docs/design/components.md` — Button, Badge, DifficultyBadge, VerdictBadge (icon + text), BudgetBar, Tabs, DataTable, EmptyState, Spinner, CodePane

### 1.1 Clerk replaces template JWT (closes KD-01)
- [ ] **1.1.1** Register/Login/Refresh slices, `RefreshToken`, `TokenProvider`, password hashing, `Jwt` config, `JWT_SECRET` deleted
- [ ] **1.1.2** `AddJwtBearer` with `Authority = Clerk:Issuer`, `MapInboundClaims = false`, lifetime + issuer validated
- [ ] **1.1.3** `azp` checked against `Clerk:AuthorizedParties` in `OnTokenValidated`
- [ ] **1.1.4** `IUserContext.ExternalId` (`sub`) + lazily resolved `UserId`, cached `users:ext:{sub}`
- [ ] **1.1.5** `Clerk:Issuer` validated at startup (fail fast)
- [ ] **1.1.6** KD-01 baseline entries removed from `scripts/check-rules.ps1`

### 1.2 Entities
- [ ] **1.2.1** `User` (`ExternalId` unique, `Role`, `Plan`, `IsDisabled`, `IsDemo`)
- [ ] **1.2.2** `Problem` (slug unique, status/version, signature, statement, starter, reference, limits jsonb, tags, sort order, `xmin`)
- [ ] **1.2.3** `TestCase` (data-driven: args/expected jsonb, compare, budgets, hidden, order)
- [ ] **1.2.4** `RolePermission`; enums `ProblemStatus`, `Track`, `Difficulty`, `Role`
- [ ] **1.2.5** `Problem.Publish()` / `Archive()` guarded; `ProblemErrors`, `UserErrors`
- [ ] **1.2.6** Configurations in `Database/Configurations/`; migration `Phase1_Problems_Users`

### 1.3 Roles & permissions
- [ ] **1.3.1** `RoleSeeder` writes learner/author/admin matrix (idempotent, every start)
- [ ] **1.3.2** `PermissionProvider` loads by `ExternalId`, cached 5 min, none for disabled users
- [ ] **1.3.3** Permission names as constants

### 1.4 User sync
- [ ] **1.4.1** Upsert-on-first-request middleware; bootstrap email → admin
- [ ] **1.4.2** `Users/HandleClerkWebhook` — Svix verified in endpoint, handler upserts/updates/soft-deletes
- [ ] **1.4.3** Webhook tested via tunnel or dashboard test event; `CLERK_WEBHOOK_SECRET` set

### 1.5 Seeding from `problems/`
- [ ] **1.5.1** `YamlDotNet`; `ProblemDefinition` matches the YAML shape
- [ ] **1.5.2** `ProblemSeeder` insert-only, seeds as Published v1
- [ ] **1.5.3** `--seed` / `--seed-demo` (Development only) flags in `Program.cs`; exits after seeding
- [ ] **1.5.4** Problems 1–4 seeded (5–6 deferred to Phases 5–6)

### 1.6 Slices
- [ ] **1.6.1** `Problems/GetProblems` — PagedList, filters, sort allow-list, Published only, cached
- [ ] **1.6.2** `Problems/GetProblem` — visible tests only, `hiddenTestCount`, no reference, Pro gating, cached
- [ ] **1.6.3** `Users/GetMe` — role, plan, permissions
- [ ] **1.6.4** `ProblemCacheKeys`; seeding invalidates `problems:*`

### 1.7 Infrastructure wiring
- [ ] **1.7.1** Redis as HybridCache L2 (dev: `localhost:16379` + password via user-secrets)
- [ ] **1.7.2** Health checks: Postgres + Redis + NATS at `/health`
- [ ] **1.7.3** CORS origins verified (app, marketing, localhost dev ports)
- [ ] **1.7.4** `--migrate` flag for Production

### 1.8 React app
- [ ] **1.8.1** Vite React-TS in `apps/web`; strict tsconfig; ESLint (ts strict, react-hooks, jsx-a11y); `build.sourcemap: false`
- [ ] **1.8.2** `config.ts` (runtime `window.__CONFIG__` + `VITE_*` fallback) and `index.html` loads `/config.js`
- [ ] **1.8.3** `lib/api.ts` — `useApi` (token per request), `ApiError` from ProblemDetails
- [ ] **1.8.4** 🎨 `tokens.css` wired into Tailwind; shared components from `components.md`; Monaco theme from tokens; light/dark
- [ ] **1.8.5** `main.tsx` providers; routes `/`, `/problems/:slug`, `/sign-in/*`, `/sign-up/*`, `/me`
- [ ] **1.8.6** Catalog page per wireframe (TanStack Query, filters)
- [ ] **1.8.7** Editor page per wireframe — statement pane + Monaco (`csharp`) + starter code + `localStorage` drafts
- [ ] **1.8.8** Dockerfile, `nginx.conf`, `docker/40-config.sh` (publishable key only)

### 1.9 Astro site
- [ ] **1.9.1** Astro minimal + tailwind + sitemap + mdx, `output: 'static'`
- [ ] **1.9.2** 🎨 Same `tokens.css`; pages per `pages.md`; all copy original
- [ ] **1.9.3** Catalog generated from `problems/*/problem.yaml` (summaries only)
- [ ] **1.9.4** Pages: home, problems, problem detail, how-it-works, pricing, blog + RSS, legal
- [ ] **1.9.5** Deep links with `?entry=`; Dockerfile + nginx

### 1.10 Compose & CI
- [ ] **1.10.1** `--profile app` builds and serves api, web, marketing through Traefik
- [ ] **1.10.2** CI frontend job enabled; Dependabot npm entries added
- [ ] **1.10.3** Hosts-file entries for `*.codebench.localhost` (PowerShell 5.1 doesn't resolve them)

### 1.11 Tests
- [ ] **1.11.1** Unit: handler + validator tests for every new slice
- [ ] **1.11.2** Integration: test JWT scheme (`sub` + `azp`) replaces register/login helpers
- [ ] **1.11.3** Integration: `GET /problems/{slug}` JSON has no hidden tests / reference
- [ ] **1.11.4** Integration: anonymous catalog 200; `GET /me` 401 anonymous, 200 signed in
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
