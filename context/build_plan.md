# CodeBench — Build Plan

A step-by-step plan for building CodeBench on the Vertical Slice Architecture template, phase by phase,
so every piece is understood rather than pasted. It is the **executable index** of the design in
`docs/guide/` — each step says what to build, where it goes, why, and which guide section has the detail.

**Revision 2 (2026-11-19):** two tracks now run through every phase —
**REA self-audits** (Appendix D, checks `RA-01…RA-09`) and an **original design system** (§1.0,
`docs/design/`). Katabench is not observed or used as a design reference (ADR 0006).

**Target stack (as set up in this repo):**

| Concern | Choice |
|---|---|
| SDK | .NET 10 (`net10.0`), analyzer-strict (`TreatWarningsAsErrors`, `AnalysisMode=All`, SonarAnalyzer) |
| Architecture | Vertical Slice Architecture — one `Web.Api` project, one file per use case |
| Mediation | Custom CQRS (`ICommand`/`IQuery` + handlers), Scrutor scanning, validation + logging decorators — no MediatR |
| Validation | FluentValidation 12 (commands only) |
| Errors | `Result` / `Result<T>` + `Error` → RFC 7807 via `CustomResults.Problem` |
| API style | Minimal APIs, one nested `Endpoint : IEndpoint` per slice, OpenAPI + Scalar |
| Database | PostgreSQL 17, EF Core 10 + Npgsql, snake_case, migrations |
| Cache | HybridCache, Redis 7 as L2 |
| Queue | NATS JetStream (work queues `JOBS`, `RESULTS`; object store `problem-bundles`) + transactional outbox |
| Code execution | Worker (no DB) → Docker-in-Docker `sandbox-daemon` → one no-network container per run; Roslyn harness |
| Auth | Clerk (dev instance) JWT validation + `azp`; roles/permissions in our DB |
| Frontend | React 19 + Vite + TS, Clerk, TanStack Query, Monaco, Tailwind v4, zod; Astro static site |
| Design | Own design system: `docs/design/` brief + tokens → Tailwind v4 `@theme`, shared by app and site |
| Edge | Traefik **v3.6** on `*.codebench.localhost`; Docker networks `edge` / `backend` / `exec` / `egress` |
| Observability | Serilog + OpenTelemetry → `grafana/otel-lgtm` (Grafana, Loki, Tempo, Prometheus) |
| Self-audit | REA CLI (`rea-agents`, 6.7.x) over **our own** DLLs, `dist/` bundles and network captures → `artifacts/rea/` |
| Tests | xUnit, Shouldly, NSubstitute, Testcontainers, NetArchTest; Vitest + Testing Library + MSW; Playwright |
| CI/CD | GitHub Actions → GHCR images → `deploy/update.sh` on your machine |

**Related documents:**
- `context/progress_tracker.md` — tick off every checkpoint; items are grouped under this plan's step numbers.
- `context/code_rules.md` — the rules your code is reviewed against (rule IDs referenced below as *[VS-04]* etc.).
- `docs/guide/00-overview.md` … `09-seed-data-and-admin.md` — the design. Where tabs disagree, **09 wins** (ADR 0005).
- `docs/adr/` — decisions (0006: no third-party recon; screenshots not a reference); `docs/api-contract.md`;
  `docs/feature-map.md`; `docs/design/` (from §1.0).
- `scripts/check-rules.ps1` (source rules) and `scripts/rea-audit.ps1` (artefact rules, Appendix D).

---

## How to use this plan

Work **top to bottom**. Each phase ends with a **Verify** block — don't move on until it passes, and run
`powershell -File scripts/check-rules.ps1` **and** (from Phase 0.4 on) `powershell -File scripts/rea-audit.ps1`
at every Verify. Phases follow `docs/guide/07-roadmap.md`; **Phase 3A** (admin console) is inserted between
3 and 4 as guide 09 directs.

Within a phase, the order is **inside-out**: entity + configuration + migration → slices → tests → UI →
audit. If you find yourself starting at the React component, you're about to invent an API. If you find
yourself starting from someone else's screenshot, you're about to copy a product.

**Estimated effort:** Phase 0–1 a weekend (+ an evening for §1.0 design); Phase 2 is the hard one
(1–2 weeks); 3/3A a week; 4–7 open-ended.

**Claude Code shortcuts** (prefer these; they encode the conventions):

| Task | Use |
|---|---|
| Use case (command/query + validator + endpoint + tests) | `/add-feature` |
| Entity + errors + events + EF config + DbSet + migration | `/add-entity`, `/add-migration` |
| Outbox publisher, results consumer, janitor | `/add-background-job` |
| React feature / Astro page | `/add-react-feature`, `/add-astro-page` (both read `docs/design/`) |
| Backfill tests | `/add-tests` |
| Review a change | `/vsa-review` + `context/code_rules.md` + `scripts/rea-audit.ps1` |
| Inspect an artefact ad hoc | REA MCP tools (after 0.4.3) — **own artefacts only** |

---

## Phase 0 — Setup & self-audit baseline  ·  guide 00, 01 ("own builds"), 07 §0, 08

**Goal:** a monorepo that builds, infra that starts, keys in hand, and REA proving the boundaries of the
assemblies that already exist.

### 0.1 Toolchain  ✅ done

```powershell
dotnet --list-sdks          # 10.0.x
node --version              # 24 LTS (REA wants 24.22.1+; 24.18 works today — upgrade when convenient)
docker info                 # Docker Desktop RUNNING, WSL2 backend, >= 8 GB RAM, Engine 29+
dotnet ef --version         # dotnet tool install -g dotnet-ef
git --version
rea --version               # npm i -g rea-agents  (6.7.x); `rea doctor`: node + host ok is enough
```

### 0.2 Bootstrap the template  ✅ done

Template renamed to `CodeBench.slnx`, Todos sample removed, clean `Initial` migration, `Web.Api`
namespace kept. Template fixes — they will bite again:
- `Decorate` → `TryDecorate` in `DependencyInjection.cs` — KD-03.
- `[**/Database/Migrations/*.cs] generated_code = true` in `.editorconfig` — KD-05.

### 0.3 Monorepo layout  ✅ done

```
apps/{marketing,web}  src/{Web.Api,Contracts,Worker,Sandbox.Runner}  sandbox-images/  problems/
tests/{Web.Api.UnitTests,IntegrationTests,ArchitectureTests,hostile-submissions}  deploy/  docs/  context/  scripts/
```

`src/Contracts` holds `Subjects`, `GradeRequested` (incl. `ProblemVersion`), `TestOutcome`,
`GradeCompleted`, `Verdicts`. `BoundaryTests` enforce AR-02/AR-03 from day one.

### 0.4 REA self-audit setup  ·  guide 01 "Using REA on your own builds", ADR 0006  ✅ done

`powershell -File scripts/rea-audit.ps1 [-Build] [-Ci] [-UpdateBaseline]` — RA-01..03 live, baseline saved.

No third-party recon (ADR 0006): the Terms review is recorded in `docs/recon.md`; `docs/feature-map.md`
and `docs/api-contract.md` are our own design. REA is used **only on CodeBench's artefacts**.

1. **Register REA with Claude Code** — `npx rea-agents@latest setup` (choose Claude Code, review the diff),
   restart. Set `REA_MCP_OUTPUT_SCHEMA_PROFILE=compact` if the tool list is too large.
2. **Evidence location** — `artifacts/rea/<check>.json` (`artifacts/` is git-ignored). Always pass
   `--format json` and narrow with `--filter-output`; token windows (`--token-limit`) are rejected for
   structured output.
3. **`scripts/rea-audit.ps1`** — same shape as `check-rules.ps1` (`ok`/`FAIL`/`skip`/`base`, non-zero exit
   on failure, `$Baseline`), one function per `RA-xx` in Appendix D. Each check **skips** until its
   artefact exists, so the script runs in every phase. Start with the three assembly checks that work today:

```powershell
dotnet build CodeBench.slnx -c Release
rea inspect-managed-artifact (Resolve-Path src/Worker/bin/Release/net10.0/CodeBench.Worker.dll) `
    --format json --filter-output normalized_result.references > artifacts/rea/worker-refs.json
# RA-01: no reference name matching Npgsql* | Microsoft.EntityFrameworkCore* | Web.Api
# REA sees DIRECT references only → also scan CodeBench.Worker.deps.json for transitive packages
```

4. **Baseline** — copy the first green run to `artifacts/rea/baseline/` (used by RA-08 drift checks; in CI
   the baseline is the last `main` run's artifact).

### 0.5 Accounts & keys  ·  guide 08  ✅ done (webhook secret in 1.4.3)

- Clerk development instance: issuer, publishable key, secret key in `deploy/.env`
  (`CLERK_PUBLISHABLE_KEY` — the `VITE_*` name belongs only in `apps/web/.env.local`).
- `deploy/.env` from `.env.example`; hex secrets (URI-safe); `POSTGRES_PASSWORD=codebench` locally.

### 0.6 Version control  ✅ done

Public repo `archerzou/codebench`, secret scanning + push protection on. `docs/designs/` (third-party
screenshots) and `artifacts/` are git-ignored.

> **Verify:** `dotnet build CodeBench.slnx -c Release` → 0 warnings; `dotnet test` green; `check-rules.ps1`
> passes; `rea-audit.ps1` → RA-01..RA-03 `ok`, the rest `skip`; `docker compose -f deploy/docker-compose.yml
> -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d` starts traefik, postgres (host 15432),
> redis (host 16379), nats, observability; REA tools visible in Claude Code.

---

## Phase 1 — Walking Skeleton  ·  guide 02 §1–3, §5, §8–9 · 04 · 05 · 09 (roles, seeding)

**Goal:** signed in through the Traefik URLs, you open a problem and see its statement and starter code —
in CodeBench's own visual design — and the shipped bundles pass the REA audit.

### 1.0 Design foundation  ·  `docs/design/`  (any time before 1.8; doesn't block 1.1–1.7)  ✅ done

Original work, derived from `docs/feature-map.md` and the API contract — **not** from screenshots of other
products (ADR 0006). Keep it small; it is a contract for the UI, not a Figma project.

| File | Contents |
|---|---|
| `docs/design/brief.md` | Audience (C# developers practising performance-aware code), 3–5 principles (e.g. *numbers first*: time/allocations are the hero; *calm editor*; *honest feedback*), voice & tone, name/wordmark direction |
| `docs/design/tokens.css` | Tailwind v4 `@theme`: colour roles (`--color-surface`, `--color-ink`, `--color-accent`, `--color-pass`, `--color-fail`, `--color-warn`, …) for light **and** dark, type scale, spacing, radius, shadow. Every text/background pair ≥ WCAG AA (record ratios) |
| `docs/design/pages.md` | Page inventory with purpose + primary action per page. Site: home, problems, problem detail, how-it-works, pricing, blog, legal. App: catalog, editor, `/me`, (7) progress, (3A) admin pages |
| `docs/design/wireframes/` | Low-fi wireframes (ASCII in markdown, or Excalidraw `.excalidraw` + PNG export) for **catalog** and **editor** first; others when their phase starts |
| `docs/design/components.md` | Shared components + states: `Button`, `Badge`, `DifficultyBadge`, `VerdictBadge` (icon + text, never colour alone *[FE-10]*), `BudgetBar`, `Tabs`, `DataTable`, `EmptyState`, `Spinner`, `CodePane` |

Both `apps/web` and `apps/marketing` import `tokens.css` (copy at build, or a tiny shared package under
`packages/design-tokens` if duplication hurts). Monaco gets a custom theme built from the same tokens.

### 1.1 Replace template JWT issuing with Clerk validation  ·  guide 02 §2, ADR 0001  · closes KD-01

**Delete:** `Features/Users/{Register,Login,Refresh,RefreshToken,AccessTokensResponse}.cs`,
`Authentication/{TokenProvider,ITokenProvider,PasswordHasher,IPasswordHasher}.cs`,
`Database/Configurations/RefreshTokenConfiguration.cs`, the `Jwt` config sections, `JWT_SECRET`,
the matching unit/integration tests, and both KD-01 baseline entries in `scripts/check-rules.ps1`.

**Add** in `Authentication/`:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        IConfigurationSection clerk = configuration.GetSection("Clerk");
        options.Authority = clerk["Issuer"];               // JWKS from {Issuer}/.well-known/jwks.json
        options.MapInboundClaims = false;                   // keep "sub"
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = false, ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents { OnTokenValidated = ValidateAuthorizedParty };   // azp ∈ Clerk:AuthorizedParties
    });
```

`IUserContext` gains `string ExternalId` (the `sub`) and keeps `Guid UserId` (+ `TryGetUserId`), resolved
once per request by `Authentication/UserResolutionMiddleware` from `db.Users` by `ExternalId` and cached in
HybridCache under `users:ext:{sub}` (misses are not cached). ✅ done 2026-11-19 — see Decisions Log. Validate `Clerk:Issuer`
at startup *[SC-06]*. **Why azp:** Clerk tokens have no audience; `azp` is what stops a token minted for
another site being accepted *[SC-02]*. Add `Svix` to `Directory.Packages.props` *[XC-01]*.

### 1.2 Entities: `User`, `Problem`, `TestCase`, roles  ·  guide 02 §3, 09 "Data model additions"

Use `/add-entity` for each. All ids `Guid`, FK ids only, no navigations *[DM-03]*.

| Entity | Fields (Phase 1) | Configuration notes |
|---|---|---|
| `User` | `Id`, `ExternalId`, `Email`, `Role`, `Plan`, `IsDisabled`, `IsDemo`, `CreatedAt` | unique `external_id` |
| `Problem` | `Id`, `Slug`, `Title`, `Track`, `Difficulty`, `IsFree`, `Status`, `Version`, `Signature`, `StatementMarkdown`, `StarterCode`, `ReferenceSolution`, `GraderKind`, `Limits` (jsonb), `Tags`, `SortOrder`, `Schema?`, `SeedSql?`, `Model?`, `PlanRules?` / `Metrics?` (jsonb) | unique `slug`; `xmin` concurrency token *[DM-07]* |
| `TestCase` | `Id`, `ProblemId`, `Name`, `Order`, `IsHidden`, `ArgsJson`, `ExpectedJson`, `Compare`, `TimeBudgetMs?`, `AllocBudgetBytes?`, `ExpectedException?` | jsonb args/expected |
| `RolePermission` | `Role`, `Permission` | composite key; seeded matrix |

Enums: `ProblemStatus { Draft, Published, Archived }`, `Track`, `Difficulty`, `Role { Learner, Author, Admin }`.
`Problem.Publish()` / `Archive()` are guarded methods *[DM-02]*. Errors in `ProblemErrors`, `UserErrors`.
Then `/add-migration Phase1_Problems_Users`.

### 1.3 Roles & permissions  ·  guide 09 "Roles and the admin account"

| Role | Permissions |
|---|---|
| `learner` | `problems:read`, `submissions:create`, `submissions:read-own` |
| `author` | learner + `admin:access`, `problems:write`, `tests:write` |
| `admin` | author + `problems:publish`, `problems:delete`, `users:manage`, `submissions:read-all`, `stats:read` |

`RoleSeeder` (idempotent, runs every start) writes the matrix. `PermissionProvider` loads permissions by
`ExternalId`, cached 5 min (`permissions:{externalId}`), and returns none for `IsDisabled` users.
Permission names are constants *[AR-08]*. Clerk's role claim is never trusted *[SC-03]*.

### 1.4 User sync  ·  guide 02 §2

- **Upsert on first authenticated request** (middleware in `Authentication/`): no row for `sub` →
  create `learner`; email ∈ `Admin:BootstrapEmails` → `admin`.
- **`Users/HandleClerkWebhook`** slice: the endpoint verifies the Svix signature on the raw body, then
  passes the parsed event to a command handler that upserts / updates / soft-deletes *[EP-03 exception]*.
  `.AllowAnonymous().ExcludeFromDescription()` *[EP-04]*. Locally, rely on the upsert; test the webhook
  with a Cloudflare quick tunnel or the dashboard's test events, then set `CLERK_WEBHOOK_SECRET`.

### 1.5 Seeding problems from `problems/`  ·  guide 09 "The seeder", "The six seed problems"

`problems/<slug>/problem.yaml` is **seed data only** (ADR 0005). Add `YamlDotNet`.

```csharp
// Database/Seeding/ProblemSeeder.cs — insert-only; an existing slug is skipped
foreach (string file in Directory.EnumerateFiles(folder, "problem.yaml", SearchOption.AllDirectories))
{
    ProblemDefinition def = deserializer.Deserialize<ProblemDefinition>(await File.ReadAllTextAsync(file, ct));
    if (await db.Problems.AnyAsync(p => p.Slug == def.Slug, ct)) { continue; }
    Problem problem = Problem.FromDefinition(def, dateTimeProvider.UtcNow);
    problem.Publish();                       // seeds go straight to Published, Version = 1
    db.Problems.Add(problem);
    await db.SaveChangesAsync(ct);
    // Phase 2: await bundles.PublishAsync(problem, ct);
}
```

`Program.cs`: `--seed` runs `RoleSeeder` + `ProblemSeeder` from `/problems` (container) or
`../../problems` (host) and exits; `--seed-demo` only in Development *[SC-05]*. Phase 1 seeds problems
**1–4**; 5–6 wait for their graders (Phases 5–6).

### 1.6 Slices  ·  guide 02 §5, `docs/api-contract.md`

| Slice | Route | Auth | Notes |
|---|---|---|---|
| `Problems/GetProblems` | `GET /problems` | `AllowAnonymous` | `PagedList<Response>`, track/difficulty/q/sort/page, Published only, cached `problems:list:{hash}` 10 min |
| `Problems/GetProblem` | `GET /problems/{slug}` | `AllowAnonymous` | visible tests only, `hiddenTestCount`, never `ReferenceSolution` *[QY-04]*; Pro tracks require permission → `NotFound`; cached `problems:{slug}` |
| `Users/GetMe` | `GET /me` | `RequireAuthorization` | id, email, role, plan, permissions[] (drives `RequirePermission` in the UI) |
| `Users/HandleClerkWebhook` | `POST /webhooks/clerk` | signature | see 1.4 |

Each via `/add-feature`; `ProblemCacheKeys` holds the keys; seeding invalidates them *[QY-05]*.

### 1.7 Infrastructure wiring  ·  guide 02 §8, 05

- `Microsoft.Extensions.Caching.StackExchangeRedis` before `AddHybridCache()`; dev connection
  `localhost:16379,password=…` via user-secrets (compose Redis has a password).
- Health checks: Postgres + Redis + NATS → `GET /health`.
- CORS from `Cors:AllowedOrigins` *[SC-04]*.
- `--migrate` flag in `Program.cs` for Production (`docker compose run --rm api --migrate`).

### 1.8 React app  ·  guide 04 "Web app", `docs/design/`

```powershell
Move-Item apps/web/README.md docs/web-notes.md   # Vite needs an empty folder
cd apps; npm create vite@latest web -- --template react-ts; cd web
npm i @clerk/clerk-react react-router-dom @tanstack/react-query @monaco-editor/react zod
npm i -D tailwindcss @tailwindcss/vite vitest @testing-library/react @testing-library/jest-dom jsdom msw eslint-plugin-jsx-a11y
```

Build in this order: `config.ts` (runtime `window.__CONFIG__` + `VITE_*` fallback) → `lib/api.ts`
(`useApi`, `ApiError`) → **design tokens + shared components from `docs/design/components.md`** →
`main.tsx` (ClerkProvider, QueryClientProvider, RouterProvider) → routes `/`, `/problems/:slug`,
`/sign-in/*`, `/sign-up/*`, `/me` → `features/problems` (catalog per wireframe) → `features/editor`
(statement pane, Monaco `csharp` with the CodeBench theme and starter code, drafts in `localStorage`).
`tsconfig` strict *[FE-01]*; ESLint per *[FE-12]*; `build.sourcemap: false` *[FE-14]*. Dockerfile +
`nginx.conf` + `docker/40-config.sh` as guide 04 (publishable key only *[FE-05]*).

### 1.9 Astro site  ·  guide 04 "Marketing site", `docs/design/`

```powershell
cd apps; npm create astro@latest marketing -- --template minimal --typescript strict
cd marketing; npx astro add tailwind sitemap mdx
```

`output: 'static'`, same `tokens.css`. `scripts/gen-problems.mjs` (run in `prebuild`) turns
`../../problems/*/problem.yaml` into `src/content/problems/*.md` — **summaries only** *[FE-13]*. Pages per
`docs/design/pages.md`: `/`, `/problems`, `/problems/[slug]`, `/how-it-works`, `/pricing`, `/blog/[slug]`
(+ RSS), `/legal/*`. All copy written by you. Deep links carry `?entry=`.

### 1.10 Compose `app` profile & CI frontend job

Uncomment the `frontend` job in `.github/workflows/ci.yml` and add `npm` entries to
`.github/dependabot.yml`. Add `127.0.0.1 app.codebench.localhost api.codebench.localhost codebench.localhost`
to the hosts file — Windows PowerShell 5.1 doesn't resolve `*.localhost`.

### 1.11 Tests

Unit: handler + validator tests for every new slice. Integration: **test JWT scheme** issuing `sub` + `azp`
*[TS-04]*; `GET /problems/{slug}` JSON contains no hidden data *[QY-04, TS-03]*; anonymous `GET /problems`
200; `GET /me` 401 anonymous / 200 signed in. Frontend: Vitest for `ProblemList` with MSW.

### 1.12 REA audits of what Phase 1 ships  ·  Appendix D

| Check | Target | Command | Passes when |
|---|---|---|---|
| RA-04 | `Web.Api.dll`, `CodeBench.Worker.dll` | `inspect-managed-members` (ldstr literals) | no `sk_`, `whsec_`, `Password=`, `pk_live_` or connection-string literals |
| RA-05 | `apps/web/dist` | `analyze-javascript-application` | no `*.map`; no `sk_`/`whsec_`/`SECRET`/`PASSWORD` literals; no hard-coded `pk_` (comes from `config.js`); route list matches `routes.tsx` |
| RA-06 | `apps/marketing/dist` | `analyze-javascript-application` + text scan | no hidden-test args/expected values or reference-solution bodies from `problems/*.yaml` |
| RA-07 | own app at `app.codebench.localhost` | `capture-browser-scenario` (open catalog → open problem) | no captured response contains `referenceSolution`, `isHidden: true` tests, or hidden args |
| RA-09 | home, catalog, editor | `capture-web-screenshot` | baseline PNG evidence saved to `artifacts/rea/visual/` |

> **Verify:** `docker compose -f deploy/docker-compose.yml --env-file deploy/.env --profile app up -d --build`;
> `docker compose … run --rm api --migrate` then `--seed`. Sign in at http://app.codebench.localhost via
> Clerk, open `sum-of-evens`, see statement + starter code in the CodeBench design (light + dark).
> `http://codebench.localhost` lists the problems and links into the app. `/health` healthy. All tests
> green, `check-rules.ps1` passes with **no KD-01 baseline**, `rea-audit.ps1` RA-01..07 + RA-09 `ok`.

---

## Phase 2 — Algorithms Grading Pipeline  ·  guide 02 §3–7, 03, 09 (data-driven tests, bundles)

**Goal:** Run and Submit on the algorithm problems return per-test verdicts with time and allocations;
the hostile suite passes; REA proves the Worker and Runner boundaries on the published output.

### 2.1 Entities  ·  guide 02 §3

| Entity | Fields | Notes |
|---|---|---|
| `Submission` | `Id`, `UserId?` (null for anonymous runs), `ProblemId`, `ProblemVersion`, `Kind` (Run/Submit), `Code`, `Status`, `Verdict?`, `CompilerOutput?`, `CreatedAt`, `GradedAt?` | index `(user_id, created_at DESC)`; `Create`, `MarkRunning`, `MarkGraded`, `MarkFailed` guarded + idempotent *[DM-02]* |
| `SubmissionTestResult` | `SubmissionId`, `TestName`, `IsHidden`, `Passed`, `ElapsedMicros`, `AllocatedBytes`, `Message?`, `Details?` (jsonb) | one row per test |
| `OutboxMessage` (`Messaging/`) | `Id`, `Type` (subject), `Payload` (jsonb), `OccurredAt`, `ProcessedAt?`, `Error?` | partial index `WHERE processed_at IS NULL` |
| `UserProblemProgress` | `UserId`, `ProblemId`, `BestVerdict`, `Attempts`, `SolvedAt?` | composite key |

`/add-migration Phase2_Submissions_Outbox`.

### 2.2 Slices  ·  guide 02 §5

| Slice | Route | Auth | Rules |
|---|---|---|---|
| `Submissions/CreateRun` | `POST /runs` | `AllowAnonymous` for free problems; signed in otherwise | visible tests only, subject `jobs.runs.created`, `anonymous-runs` limit (5/min/IP) |
| `Submissions/CreateSubmission` | `POST /submissions` | `RequireAuthorization` | all tests, subject `jobs.submissions.created`, ≤ 2 in flight → `Submissions.TooManyInFlight`, `submissions` limit (10/min/user) |
| `Submissions/GetSubmission` | `GET /submissions/{id}` | owner (anonymous run: unguessable id) | hidden results redacted *[QY-04, EP-07]* |
| `Submissions/GetMySubmissions` | `GET /me/submissions` | signed in | `PagedList` |

Both creates: validator (`ProblemId` not empty, `Code` ≤ 64 000), problem must be Published, write
`Submission` + `OutboxMessage(GradeRequested{…, ProblemVersion})` in **one** `SaveChangesAsync` *[MQ-01]*,
`.WithIdempotency()`, `Results.Accepted("/submissions/{id}", new { id })` *[EP-06]*.

### 2.3 NATS JetStream  ·  guide 02 §6, 09 "Problem bundles"

`NATS.Net` (API + Worker), one `NatsClient` singleton per process. `Messaging/JetStreamSetup` (hosted,
idempotent): stream `JOBS` (`jobs.>`, work-queue, 1 h), stream `RESULTS` (`results.>`, work-queue), object
store `problem-bundles`. Verify method names against current NATS.Net docs.

### 2.4 Problem bundles  ·  guide 09

`Messaging/ProblemBundlePublisher`: serialise `{ signature, tests (incl. hidden), schema, seedSql, model,
planRules, metrics, limits }` to `{slug}/v{version}` — never overwrite *[MQ-06]*. Back-fill seeded problems.

### 2.5 Outbox publisher  ·  guide 02 §7, ADR 0003

`Messaging/OutboxPublisher`: every 500 ms, one transaction, `SELECT … FOR UPDATE SKIP LOCKED LIMIT 50`,
publish with `MsgId = msg.Id` *[MQ-02, MQ-04]*, set `ProcessedAt` from `IDateTimeProvider` or store `Error`.

### 2.6 Results consumer  ·  guide 02 §7

`Messaging/GradeResultConsumer`: durable `api-results`; already final? ack *[MQ-03]* → `MarkGraded` +
`SubmissionTestResult` rows + `UserProblemProgress` upsert → save → remove `progress:{userId}` → ack.

### 2.7 Sandbox.Runner harness  ·  guide 03, 09 "Data-driven test format"

`Microsoft.CodeAnalysis.CSharp`. `Harness/` (stdin, nonce writer), `Compilation/` (Roslyn + allow-list +
collectible ALC), `Tests/` (arg binding, compare modes `exact`/`unordered`/`unorderedNested`/`approx`,
seeded generators, `$ref: reference`), `Graders/Algorithms/` (median of 7, budgets). Reject code > 64 KB;
print JSON between nonce markers *[SB-04, SB-05]*. Unit-test compare modes and generators.

### 2.8 Algorithms sandbox image  ·  guide 03 "Sandbox images"

`sandbox-images/algorithms.Dockerfile`: SDK stage publishes `src/Sandbox.Runner` (+ `src/Contracts`);
runtime stage, `USER 10001`, `WORKDIR /tmp`, `ENTRYPOINT ["dotnet", "/opt/runner/CodeBench.Sandbox.Runner.dll",
"--grader", "algorithms"]`. No problems copied in (ADR 0005).

```powershell
docker build -f sandbox-images/algorithms.Dockerfile -t codebench-sandbox-algorithms:dev .
docker save codebench-sandbox-algorithms:dev | docker compose -f deploy/docker-compose.yml exec -T sandbox-daemon docker load
```

### 2.9 Worker  ·  guide 03 "The worker consumer", "Starting a sandbox"

`Docker.DotNet`. `SandboxOptions`, `BundleCache`, `SandboxRunner` (all hardening flags *[SB-01]*; stdin →
start → `CancelAfter` → kill → ≤ 1 MB stdout → nonce block → `finally` force-remove *[SB-03]*),
`SubmissionConsumer` (durable `graders`, `MaxAckPending = MaxParallel`, `MaxDeliver = 3`, publish
`GradeCompleted` with `MsgId = graded-{id}`, then ack), `SandboxJanitor`, image pull at start-up. Own
`IDateTimeProvider`. No DB anything *[AR-02]*.

### 2.10 Hostile-submission suite  ·  guide 03 checklist, 06

`tests/hostile-submissions/<case>/{Solution.cs,expected.json}`: network, `while(true)`, 2 GB allocation,
process spawning, writes to `/etc` and `/opt`, fake result JSON, correct O(n) solution.
`scripts/test-sandbox.sh` runs each with the worker's exact flags.

### 2.11 UI  ·  guide 04 "Run, Submit and polling", `docs/design/`

Wireframe the results panel first (`docs/design/wireframes/results.md`). `useSubmitCode('runs' |
'submissions')` with `Idempotency-Key` *[FE-07]*, `useSubmissionResult` polling 750 ms *[FE-03]*;
Run/Submit buttons (Ctrl/Cmd+Enter); results panel with `VerdictBadge` + `BudgetBar` (µs, bytes,
`aria-live`) *[FE-10]*; compiler diagnostics → Monaco markers; signed-out Submit → sign-in → resubmit.

### 2.12 Tests

Integration (Testcontainers Postgres **and** NATS `nats:2 -js`): submission + outbox row; publisher emits on
`jobs.submissions.created`; fake `GradeCompleted` grades + updates progress (twice = idempotent); third
in-flight → 400; anonymous Run on non-free → 401; foreign submission → 404. Arch: Worker still DB-free.

### 2.13 REA audits of the grading pipeline  ·  Appendix D

| Check | Target | Passes when |
|---|---|---|
| RA-01 (full) | `dotnet publish src/Worker -c Release` output | no `Npgsql*` / `Microsoft.EntityFrameworkCore*` / `Web.Api` in direct refs (`inspect-managed-artifact`) **or** `deps.json`; config keys read are only `ConnectionStrings:Nats`, `Sandbox:*`, `OTEL_*`, `DOCKER_HOST` (`inspect-managed-members` ldstr) |
| RA-03 (full) | published `CodeBench.Sandbox.Runner.dll` (from the image) | refs ⊆ `System.*`, `CodeBench.Contracts`, `Microsoft.CodeAnalysis*` |
| RA-07 (ext.) | own app: Run → result | `GET /submissions/{id}` hidden results expose only name/passed/timings/generic message |
| RA-10 *(optional)* | hostile suite under WSL2/CI Linux | `rea capture-process` filesystem/exit observations match `expected.json` (REA process capture is Linux/macOS only) |

> **Verify:** `--profile app --profile grading` up, image loaded: `sum-of-evens` reference → Accepted with
> per-test µs and bytes; LINQ fails the 1 KB budget; `while(true)` → Timeout; hostile suite passes;
> `exec sandbox-daemon docker ps -a` empty; `dotnet test` green; `check-rules.ps1` (SB-01 active) and
> `rea-audit.ps1` pass.

---

## Phase 3 — CI/CD  ·  guide 06

**Goal:** a merge to `main` produces versioned images; `update.sh` deploys them locally; every PR gets an
REA audit report.

### 3.1 `ci.yml`
dotnet job; frontend matrix (web, marketing): lint, test, build; `pwsh scripts/check-rules.ps1`.

### 3.2 `images.yml`
Matrix `api`, `worker`, `web`, `marketing` → `ghcr.io/<owner>/codebench-<name>:<sha>` + `:latest`, GHA cache,
lowercase owner, gated on green CI.

### 3.3 `sandbox-images.yml`
On `src/Sandbox.Runner/**`, `src/Contracts/**`, `sandbox-images/**`: build with `load: true`, run the
hostile suite, push only on `main`.

### 3.4 `e2e.yml`
Nightly + manual: `deploy/.env.ci`, `compose up --wait`, Playwright smoke, upload report + compose logs.

### 3.5 Repository settings
Branch protection on `main`, Dependabot (nuget, npm, docker, github-actions), secret scanning + push
protection (already on), pin third-party actions.

### 3.6 Deploy & rollback
`deploy/update.sh` (+ `update.ps1`): `pull` → `run --rm api --migrate` → `up -d --remove-orphans` →
`image prune`. `TAG=<sha>` pins/rolls back. GHCR login with a `read:packages` PAT.

### 3.7 `rea-audit` CI job  ·  Appendix D
After `dotnet publish` + `npm run build` (ubuntu-latest, Node 24): `npm i -g rea-agents@<pinned>` →
`pwsh scripts/rea-audit.ps1 -Ci` → upload `artifacts/rea/` as an artifact. Fails on any `FAIL`.
**RA-08 drift**: download the last `main` run's `artifacts/rea/` as baseline; `compare-managed-members`
(API, Worker, Runner) and `compare-application-versions` (web) → summary in `$GITHUB_STEP_SUMMARY`
(informational, not failing). **RA-09** visual diff on PRs touching `apps/**`
(`compare-web-screenshots --channel-threshold 8`), images attached to the run.

> **Verify:** PR → CI green incl. `rea-audit` with its artifact; merge → four images in GHCR tagged by SHA;
> `./deploy/update.sh` brings them up; `./deploy/update.sh <previous-sha>` rolls back.

---

## Phase 3A — Admin Console  ·  guide 09 "Admin API", "Admin UI"

**Goal:** an admin can create a problem, add tests, validate it against the reference, publish it, and a
learner can solve it — with no image rebuild — and the learner bundle contains no admin code.

### 3A.1 Admin route group
`app.MapGroup("admin").RequireAuthorization().HasPermission(Permissions.AdminAccess).WithTags(Tags.Admin)`;
slices in `Features/Admin/` with their extra permission per endpoint *[EP-04]*.

### 3A.2 Problems
`ListProblems`, `GetProblemForEdit`, `CreateProblem` (Draft), `UpdateProblem` (`If-Match` → 409 *[DM-07]*;
editing a Published problem edits a draft copy *[DM-08]*), `DeleteProblem` (Drafts only), `ArchiveProblem`.

### 3A.3 Tests
`CreateTestCase`, `UpdateTestCase`, `DeleteTestCase`, `ReorderTestCases`.

### 3A.4 Validate → Publish
`ValidateProblem` runs the **reference solution** through the real worker; `PublishProblem` refuses unless
validation passed (409 `Problems.NotValidated`), then `Version++`, writes `{slug}/v{n}`, Published,
invalidates `problems:*`, audits.

### 3A.5 Import / export
`ImportProblemYaml` / `ExportProblemYaml` (same YAML shape as `problems/`).

### 3A.6 Users, submissions, audit, stats
`ListUsers`, `ChangeUserRole`, `DisableUser`; `ListAllSubmissions`, `GetAnySubmission`, `RegradeSubmission`;
`AuditLog` + `SaveChangesInterceptor`; `ListAuditLog`; `GetAdminStats` (cached 60 s).

### 3A.7 Demo data
`DemoDataSeeder` (`--seed-demo`, Development only) *[SC-05]*.

### 3A.8 Admin UI  ·  `docs/design/`
Wireframes for dashboard, problems table and problem editor in `docs/design/wireframes/admin-*.md`
first. Lazy `/admin` behind `RequirePermission permission="admin:access"` *[FE-08]*; add
`@tanstack/react-table`, `react-hook-form`, `@hookform/resolvers`, `recharts`, `react-markdown`. Pages:
Dashboard, Problems table, Problem editor (Details · Statement · Code · Tests grid · Rules · Preview;
Save draft · Validate · Publish), Users, Submissions, Audit log — built from the shared components.

### 3A.9 Tests  ·  guide 09 "Tests for the admin side"

### 3A.10 REA audits of the admin split
**RA-05 (ext.)**: admin code lives only in lazy chunks — the learner entry chunk contains no
`/admin` routes or admin component names. **RA-07 (ext.)**: a learner browser scenario never requests
`/admin/*`; hidden tests / reference appear only in admin-route responses.

> **Verify:** guide 09 test list passes — learner 403 on every `/admin/*`; author edits but can't publish;
> admin publishes; publish without validation → 409; stale `If-Match` → 409; `GET /problems/{slug}` never
> contains hidden tests/reference; publish writes `{slug}/v{n}`; E2E admin → learner; `rea-audit.ps1` passes.

---

## Phase 4 — Observability & Hardening  ·  guide 03 checklist, 05, 07 §4

### 4.1 Trace context through NATS *[MQ-07]*
W3C `traceparent` in NATS headers on publish (outbox, worker), extracted on consume; one trace spans
API → NATS → worker → result.

### 4.2 Dashboards & alerts
Submissions/min, grading latency p50/p95, verdict mix, `JOBS` depth, sandbox failures, DinD disk alert.
Decide on splitting `otel-lgtm` (KD-04).

### 4.3 Sandbox hardening
Janitor verified; gVisor (`runsc`) on Linux/WSL2 native engine; Redis/NATS passwords enforced; guide 03
checklist fully ticked.

### 4.4 Release audit gate (REA)
Audits now run on **what ships**: extract the published files from the GHCR images
(`docker create` + `docker cp`) and run the full `rea-audit.ps1 -FromImages <sha>`. Record
`compare-managed-members` / `compare-application-versions` between the previous and new release SHA in
the release notes. A failing RA blocks `update.sh` for that SHA.

### 4.5 Backups
`--profile backup`; restore one backup into a scratch database end to end.

> **Verify:** one trace in Tempo shows API → worker → result; dashboards populated; restored backup
> contains your problems; release audit attached to the latest release.

---

## Phase 5 — Database / EF Core Grader  ·  guide 03 "Database grader", 09 problem 5

### 5.1 Database sandbox image
`sandbox-images/database.Dockerfile` + `db-entrypoint.sh`: Postgres in `/tmp/pgdata` (tmpfs ~256 MB),
Unix socket only, pre-initialised data dir.

### 5.2 Harness
`Graders/Database/`: bundle `schema` + `seedSql` + `ANALYZE`; `DbContext` from `model`;
`DbCommandInterceptor`; compare rows; `EXPLAIN (FORMAT JSON)` per statement.

### 5.3 Plan rules
`noSeqScan`, `usesIndex`, `maxRoundTrips`; results in `Details`.

### 5.4 UI
Expandable SQL + plan summary + rule outcomes per test (wireframe first).

### 5.5 Problems
Seed `customers-since`, then four originals: N+1, filter-before-fetch, sargable date ranges,
projection, pagination.

### 5.6 REA
**RA-03** allow-list extended for the database image only: `+ Microsoft.EntityFrameworkCore*`, `Npgsql*`.

> **Verify:** `customers-since` reference → Accepted; in-memory filter and N+1 fail `one-trip`; SQL and
> plan visible in the UI; RA-03 ok for both images.

---

## Phase 6 — More Graders  ·  guide 03 "Other graders", 09 problem 6

| Step | Grader | Mechanism |
|---|---|---|
| 6.1 | Refactoring | behaviour tests + Roslyn metrics vs per-problem thresholds; seed `shipping-cost-refactor` |
| 6.2 | Architecture | multi-file submissions in separate assemblies; NetArchTest rules |
| 6.3 | Secure coding | normal tests + adversarial suite |
| 6.4 | Test writing | user tests vs correct implementation and planted mutants |

Each grader: harness code, image (or flag), hostile-suite cases, one original problem, UI for its
`Details`, and its **RA-03** allow-list entry.

> **Verify:** one problem per grader: reference → Accepted, known-bad → rejected for the right reason.

---

## Phase 7 — Product Polish  ·  guide 07 §7, 02 §5

### 7.1 Progress
`Progress/GetProgress` (`GET /me/progress`), cached `progress:{userId}`; catalog checkmarks; progress page
designed in `docs/design/` first.

### 7.2 Learning paths
Ordered problem lists with prerequisites (entity + admin CRUD + learner view).

### 7.3 Plans & permissions
Free vs Pro tracks mapped to permissions; `GetProblem` enforces.

### 7.4 Payments (sandbox only)
Paddle **or** Stripe test mode; checkout with the public token only; signed webhook slice changes
`User.Plan`. **RA-05 (ext.)**: only the public payment token appears in the bundle. Not tax advice.

### 7.5 Optional public preview
Cloudflare Tunnel + Clerk production instance on your own domain.

> **Verify:** a sandbox payment flips a user to Pro and unlocks a Pro track; progress and streaks render.

---

## Appendix A — The repeatable feature loop

```
1. ENTITY      Features/Bookmarks/Bookmark.cs (+ BookmarkErrors, domain event if needed)   /add-entity
2. CONFIG      Database/Configurations/BookmarkConfiguration.cs (unique user_id+problem_id)
3. MIGRATION   /add-migration Phase7_Bookmarks                                               [NM-09]
4. SLICE       Features/Bookmarks/AddBookmark.cs — Command, Validator, Handler, Endpoint   /add-feature
5. CACHE       BookmarkCacheKeys; invalidate in the command                                [QY-05]
6. TESTS       unit (handler failure paths + happy path), validator, integration          [TS-01]
7. CONTRACT    docs/api-contract.md row
8. DESIGN      docs/design/pages.md row (+ wireframe if it's a new screen)
9. FRONTEND    features/bookmarks/useBookmarks.ts (TanStack Query) + component             [FE-03]
10. VERIFY     dotnet test · check-rules.ps1 · rea-audit.ps1 · npm test · /vsa-review
```

Zero edits to `Program.cs` / `DependencyInjection.cs` *[VS-03]*.

## Appendix B — Daily run

```powershell
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d
dotnet watch --project src/Web.Api            # http://localhost:5000, Scalar at /scalar
dotnet watch --project src/Worker             # needs DOCKER_HOST=tcp://127.0.0.1:2375 (--profile grading)
npm --prefix apps/web run dev                 # http://localhost:5173
npm --prefix apps/marketing run dev           # http://localhost:4321
powershell -File scripts/rea-audit.ps1        # before pushing anything that changes a shipped artefact
```

Postgres `localhost:15432`, Redis `localhost:16379`, Grafana http://localhost:3000 ·
JetStream: `docker run --rm --network codebench_backend natsio/nats-box nats -s nats://codebench:<pw>@nats:4222 stream ls`.

## Appendix C — Troubleshooting  ·  guide 05

| Symptom | Likely cause |
|---|---|
| 404 from Traefik | router label typo / container not on `edge`; or Traefik < v3.6 on Docker Engine 29+ (logs: `Error response from daemon: ""`) |
| `ports are not available … 6379` on `up` | Windows WinNAT reserved range — dev override uses 16379 (and 15432 for Postgres) |
| CORS error | origin missing from `Cors:AllowedOrigins` |
| 401 everywhere | wrong `Clerk:Issuer` or `azp` not in `AuthorizedParties` |
| Runs stay Pending | worker can't reach NATS or sandbox daemon; image not loaded in DinD; outbox `error` column |
| Integration tests time out | Docker Desktop not running |
| Noisy timings | power saving / load — median of 7, budgets with headroom, `DOTNET_TieredPGO=0` |
| REA `UNSUPPORTED_OUTPUT_COMBINATION` | `--token-limit` with structured output — use `--format json --filter-output …` |
| REA misses a forbidden package | it lists **direct** assembly refs only — scan `*.deps.json` too |
| REA browser commands fail | no Chrome-family browser / CDP endpoint; start Chrome with `--remote-debugging-port=9222` |

## Appendix D — REA self-audit checks (`scripts/rea-audit.ps1`)

Own artefacts only. Each check writes `artifacts/rea/<id>-<target>.json` and prints `ok` / `FAIL` /
`skip` (artefact not built yet) / `base` (baselined, with a KD row in `code_rules.md`).

| ID | Sev. | Target | REA command(s) | Rule it proves | Active from |
|---|---|---|---|---|---|
| RA-01 | 🔴 | Worker DLL + `deps.json` | `inspect-managed-artifact`, `inspect-managed-members` | AR-02: no Npgsql / EF / Web.Api; only expected config keys | 0.4 (refs) · 2.13 (publish + keys) |
| RA-02 | 🔴 | Contracts DLL | `inspect-managed-artifact` | AR-03: refs ⊆ `System.*` | 0.4 |
| RA-03 | 🔴 | Sandbox.Runner DLL (per image) | `inspect-managed-artifact` | AR-04: refs ⊆ allow-list per grader | 0.4 · 2.13 · 5.6 · 6.x |
| RA-04 | 🔴 | API + Worker DLLs | `inspect-managed-members` (ldstr) | SC-01: no secret-shaped literals | 1.12 |
| RA-05 | 🔴 | `apps/web/dist` | `analyze-javascript-application` | FE-05/FE-14/FE-08: no secrets, no source maps, admin only in lazy chunks | 1.12 · 3A.10 · 7.4 |
| RA-06 | 🔴 | `apps/marketing/dist` | `analyze-javascript-application` + text scan | FE-13 / QY-04: summaries only | 1.12 |
| RA-07 | 🔴 | own running app | `capture-browser-scenario` (or HAR → `inspect-web-network-capture` with `--sensitive-value` for tokens) | QY-04 / EP-07 over real HTTP | 1.12 · 2.13 · 3A.10 |
| RA-08 | 🔵 | all of the above vs baseline | `compare-managed-members`, `compare-application-versions` | drift is reviewed, not accidental | 3.7 |
| RA-09 | 🔵 | key pages | `capture-web-screenshot`, `compare-web-screenshots` | design consistency (visual regression) | 1.12 · 3.7 |

Captures of our own app may contain Clerk session tokens: keep them under `artifacts/` or `captures/`
(both git-ignored), pass known secrets with `--sensitive-value`, delete after the run.
