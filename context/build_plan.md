# CodeBench — Build Plan

A step-by-step plan for building CodeBench on the Vertical Slice Architecture template, phase by phase,
so every piece is understood rather than pasted. It is the **executable index** of the design in
`docs/guide/` — each step says what to build, where it goes, why, and which guide section has the detail.

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
| Edge | Traefik v3 on `*.codebench.localhost`; Docker networks `edge` / `backend` / `exec` / `egress` |
| Observability | Serilog + OpenTelemetry → `grafana/otel-lgtm` (Grafana, Loki, Tempo, Prometheus) |
| Tests | xUnit, Shouldly, NSubstitute, Testcontainers, NetArchTest; Vitest + Testing Library + MSW; Playwright |
| CI/CD | GitHub Actions → GHCR images → `deploy/update.sh` on your machine |

**Related documents:**
- `context/progress_tracker.md` — tick off every checkpoint; items are grouped under this plan's step numbers (e.g. 1.6 → 1.6.1–1.6.4).
- `context/code_rules.md` — the rules your code is reviewed against (rule IDs referenced below as *[VS-04]* etc.).
- `docs/guide/00-overview.md` … `09-seed-data-and-admin.md` — the design. Where tabs disagree, **09 wins** (ADR 0005).
- `docs/adr/` — decisions; `docs/api-contract.md` — endpoint shapes; `docs/feature-map.md` — features by phase.

---

## How to use this plan

Work **top to bottom**. Each phase ends with a **Verify** block — don't move on until it passes, and run
`powershell -File scripts/check-rules.ps1` at every Verify. Phases follow `docs/guide/07-roadmap.md`;
**Phase 3A** (admin console) is inserted between 3 and 4 as guide 09 directs.

Within a phase, the order is **inside-out**: entity + configuration + migration → slices → tests → UI.
If you find yourself starting at the React component, you're about to invent an API.

**Estimated effort:** Phase 0–1 a weekend; Phase 2 is the hard one (1–2 weeks); 3/3A a week; 4–7 open-ended.

**Claude Code shortcuts** (prefer these; they encode the conventions):

| Task | Use |
|---|---|
| Use case (command/query + validator + endpoint + tests) | `/add-feature` |
| Entity + errors + events + EF config + DbSet + migration | `/add-entity`, `/add-migration` |
| Outbox publisher, results consumer, janitor | `/add-background-job` |
| Backfill tests | `/add-tests` |
| Review a change | `/vsa-review` + `context/code_rules.md` |

---

## Phase 0 — Recon & Setup  ·  guide 00, 01, 07 §0, 08

**Goal:** a monorepo that builds, recon notes in your own words, accounts and keys in hand.

### 0.1 Toolchain

```powershell
dotnet --list-sdks          # 10.0.x
node --version              # 24 LTS (also satisfies REA: 22.23.4+ / 24.22.1+)
docker info                 # Docker Desktop RUNNING, WSL2 backend, >= 8 GB RAM, privileged containers allowed
dotnet ef --version         # dotnet tool install -g dotnet-ef
git --version
```

### 0.2 Bootstrap the template  ✅ done

Template renamed to `CodeBench.slnx`, Todos sample removed, clean `Initial` migration, `Web.Api`
namespace kept. Two template fixes were needed — record them, they will bite again:
- `Decorate` → `TryDecorate` in `DependencyInjection.cs` (throws when a handler shape has no implementations) — KD-03.
- `[**/Database/Migrations/*.cs] generated_code = true` in `.editorconfig` (tool output fails IDE0161) — KD-05.

### 0.3 Monorepo layout  ✅ done

```
apps/{marketing,web}  src/{Web.Api,Contracts,Worker,Sandbox.Runner}  sandbox-images/  problems/
tests/{Web.Api.UnitTests,IntegrationTests,ArchitectureTests,hostile-submissions}  deploy/  docs/  context/  scripts/
```

`src/Contracts` holds `Subjects`, `GradeRequested` (incl. `ProblemVersion`), `TestOutcome`,
`GradeCompleted`, `Verdicts`. `BoundaryTests` enforce AR-02/AR-03 from day one.

### 0.4 Recon (observation only)  ·  guide 01

1. Read Katabench's Terms and Acceptable Use.
2. `npx rea-agents setup`, restart Claude Code.
3. Run the three recon prompts (marketing site, app shell, auth hand-off) — normal browsing, no replayed
   or modified requests, response **shapes** only.
4. Write `docs/recon.md`, then fill the "Observed" column of `docs/feature-map.md` **in your own words**,
   and adjust `docs/api-contract.md` if recon changes your design.

### 0.5 Accounts & keys  ·  guide 08

- Clerk development instance: email + GitHub sign-in; note **Issuer** (Frontend API URL), **publishable
  key**, **secret key**, and (after Phase 1 webhook) **webhook signing secret**.
- `cp deploy/.env.example deploy/.env`; generate `POSTGRES_PASSWORD` (use `codebench` locally to match
  `appsettings.Development.json`, or override via user-secrets), `REDIS_PASSWORD`, `NATS_PASSWORD`,
  `GRAFANA_ADMIN_PASSWORD`, `JWT_SECRET` (temporary, KD-01); set `ADMIN_BOOTSTRAP_EMAIL`.

### 0.6 Version control

```powershell
git init; git add .; git status        # confirm deploy/.env, bin/, obj/ are NOT staged
git commit -m "chore: bootstrap CodeBench from the VSA template"
```

Create the GitHub repo, push, enable secret scanning + push protection.

> **Verify:** `dotnet build CodeBench.slnx -c Release` → 0 warnings; `dotnet test CodeBench.slnx` green
> (Docker running); `scripts/check-rules.ps1` passes; `docker compose -f deploy/docker-compose.yml
> -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d` starts traefik, postgres, redis, nats,
> observability; `docs/recon.md` and `docs/feature-map.md` written.

---

## Phase 1 — Walking Skeleton  ·  guide 02 §1–3, §5, §8–9 · 04 · 05 · 09 (roles, seeding)

**Goal:** signed in through the Traefik URLs, you open a problem and see its statement and starter code.

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

`IUserContext` gains `string ExternalId` (the `sub`) and keeps `Guid UserId`, resolved lazily from
`db.Users` by `ExternalId` and cached in HybridCache under `users:ext:{sub}`. Validate `Clerk:Issuer`
at startup *[SC-06]*. **Why azp:** Clerk tokens have no audience; `azp` is what stops a token minted for
another site being accepted *[SC-02]*.

Add `Svix` to `Directory.Packages.props` (versions only there *[XC-01]*).

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

- **Upsert on first authenticated request** (small middleware in `Authentication/`): no row for `sub` →
  create `learner`; email ∈ `Admin:BootstrapEmails` → `admin`.
- **`Users/HandleClerkWebhook`** slice: the endpoint verifies the Svix signature on the raw body, then
  passes the parsed event to a command handler that upserts / updates / soft-deletes *[EP-03 exception]*.
  `.AllowAnonymous().ExcludeFromDescription()` *[EP-04]*. Locally, rely on the upsert; test the webhook
  with a Cloudflare quick tunnel or the dashboard's test events.

### 1.5 Seeding problems from `problems/`  ·  guide 09 "The seeder", "The six seed problems"

`problems/<slug>/problem.yaml` is **seed data only** — loaded once, then the database owns the content
(ADR 0005). Add `YamlDotNet`.

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
`../../problems` (host) and exits; `--seed-demo` only in Development *[SC-05]*. Compose already mounts
`../problems:/problems:ro` on `api`. Phase 1 seeds problems **1–3** (algorithms); 4 can go in too; 5–6
wait for their graders (Phases 5–6).

### 1.6 Slices  ·  guide 02 §5, `docs/api-contract.md`

| Slice | Route | Auth | Notes |
|---|---|---|---|
| `Problems/GetProblems` | `GET /problems` | `AllowAnonymous` | `PagedList<Response>`, track/difficulty/q/sort/page, Published only, cached `problems:list:{hash}` 10 min |
| `Problems/GetProblem` | `GET /problems/{slug}` | `AllowAnonymous` | visible tests only, `hiddenTestCount`, never `ReferenceSolution` *[QY-04]*; Pro tracks require permission → `NotFound`; cached `problems:{slug}` |
| `Users/GetMe` | `GET /me` | `RequireAuthorization` | id, email, role, plan, permissions[] (drives `RequirePermission` in the UI) |
| `Users/HandleClerkWebhook` | `POST /webhooks/clerk` | signature | see 1.4 |

Each via `/add-feature`; `ProblemCacheKeys` holds the keys; seeding invalidates them *[QY-05]*.

### 1.7 Infrastructure wiring  ·  guide 02 §8, 05

- `Microsoft.Extensions.Caching.StackExchangeRedis`: `AddStackExchangeRedisCache(o => o.Configuration =
  GetConnectionString("Redis"))` before `AddHybridCache()`.
- Health checks: Postgres (exists) + Redis + NATS → `GET /health` *[guide 05 smoke check]*.
- CORS from `Cors:AllowedOrigins` (already configured) *[SC-04]*.
- `--migrate` flag in `Program.cs` for Production (`docker compose run --rm api --migrate`).

### 1.8 React app  ·  guide 04 "Web app"

```powershell
Move-Item apps/web/README.md docs/web-notes.md   # Vite needs an empty folder
cd apps; npm create vite@latest web -- --template react-ts; cd web
npm i @clerk/clerk-react react-router-dom @tanstack/react-query @monaco-editor/react zod
npm i -D tailwindcss @tailwindcss/vite vitest @testing-library/react @testing-library/jest-dom jsdom msw eslint-plugin-jsx-a11y
```

Build in this order: `config.ts` (runtime `window.__CONFIG__` + `VITE_*` fallback) → `lib/api.ts`
(`useApi`, `ApiError` from ProblemDetails) → `main.tsx` (ClerkProvider, QueryClientProvider,
RouterProvider) → routes `/`, `/problems/:slug`, `/sign-in/*`, `/sign-up/*`, `/me` →
`features/problems` (catalog + filters) → `features/editor` (statement pane, Monaco `csharp` with starter
code, drafts in `localStorage`). `tsconfig` strict *[FE-01]*; ESLint per *[FE-12]*. Dockerfile + `nginx.conf`
+ `docker/40-config.sh` exactly as guide 04 (publishable key only *[FE-05]*).

### 1.9 Astro site  ·  guide 04 "Marketing site"

```powershell
cd apps; npm create astro@latest marketing -- --template minimal --typescript strict
cd marketing; npx astro add tailwind sitemap mdx
```

`output: 'static'`. A `scripts/gen-problems.mjs` (run in `prebuild`) turns `../../problems/*/problem.yaml`
into `src/content/problems/*.md` — **summaries only** *[FE-13]*. Pages: `/`, `/problems`,
`/problems/[slug]`, `/how-it-works`, `/pricing`, `/blog/[slug]` (+ RSS), `/legal/*`. Deep links carry
`?entry=`.

### 1.10 Compose `app` profile & CI frontend job

Uncomment the `frontend` job in `.github/workflows/ci.yml` (now there are `package-lock.json` files) and
add `npm` entries to `.github/dependabot.yml`. Add `127.0.0.1 api.codebench.localhost` etc. to the hosts
file if CLI tools don't resolve `*.localhost`.

### 1.11 Tests

Unit: handler + validator tests for every new slice. Integration: replace the template's register/login
helpers in `BaseIntegrationTest` with a **test JWT scheme** issuing `sub` + `azp` *[TS-04]*; assert
`GET /problems/{slug}` JSON contains no hidden data *[QY-04, TS-03]*; anonymous `GET /problems` 200;
`GET /me` 401 anonymous / 200 signed in. Frontend: Vitest for `ProblemList` with MSW.

> **Verify:** `docker compose -f deploy/docker-compose.yml --env-file deploy/.env --profile app up -d --build`;
> `docker compose … run --rm api --migrate` then `--seed`. Sign in at http://app.codebench.localhost via
> Clerk, open `sum-of-evens`, see statement + starter code. `http://codebench.localhost` lists the problems
> and links into the app. `/health` healthy. All tests green, `check-rules.ps1` passes with **no KD-01
> baseline entries left**.

---

## Phase 2 — Algorithms Grading Pipeline  ·  guide 02 §3–7, 03, 09 (data-driven tests, bundles)

**Goal:** Run and Submit on the algorithm problems return per-test verdicts with time and allocations;
the hostile suite passes.

### 2.1 Entities  ·  guide 02 §3

| Entity | Fields | Notes |
|---|---|---|
| `Submission` | `Id`, `UserId?` (null for anonymous runs), `ProblemId`, `ProblemVersion`, `Kind` (Run/Submit), `Code`, `Status`, `Verdict?`, `CompilerOutput?`, `CreatedAt`, `GradedAt?` | index `(user_id, created_at DESC)`; `Create`, `MarkRunning`, `MarkGraded`, `MarkFailed` guarded + idempotent *[DM-02]* |
| `SubmissionTestResult` | `SubmissionId`, `TestName`, `IsHidden`, `Passed`, `ElapsedMicros`, `AllocatedBytes`, `Message?`, `Details?` (jsonb) | one row per test |
| `OutboxMessage` (`Messaging/`) | `Id`, `Type` (subject), `Payload` (jsonb), `OccurredAt`, `ProcessedAt?`, `Error?` | partial index `WHERE processed_at IS NULL` |

`/add-migration Phase2_Submissions_Outbox`.

### 2.2 Slices  ·  guide 02 §5

| Slice | Route | Auth | Rules |
|---|---|---|---|
| `Submissions/CreateRun` | `POST /runs` | `AllowAnonymous` for free problems; signed in otherwise | visible tests only (`IncludeHidden: false`), subject `jobs.runs.created`, `anonymous-runs` limit (5/min/IP) |
| `Submissions/CreateSubmission` | `POST /submissions` | `RequireAuthorization` | all tests, subject `jobs.submissions.created`, ≤ 2 in flight → `Submissions.TooManyInFlight`, `submissions` limit (10/min/user) |
| `Submissions/GetSubmission` | `GET /submissions/{id}` | owner (anonymous run: unguessable id) | hidden results redacted *[QY-04, EP-07]* |
| `Submissions/GetMySubmissions` | `GET /me/submissions` | signed in | `PagedList` |

Both creates: validator (`ProblemId` not empty, `Code` ≤ 64 000), problem must be Published, write
`Submission` + `OutboxMessage(GradeRequested{…, ProblemVersion})` in **one** `SaveChangesAsync` *[MQ-01]*,
`.WithIdempotency()`, `Results.Accepted("/submissions/{id}", new { id })` *[EP-06]*.
Add policies to `RateLimitingPolicies` + `RateLimitingExtensions`.

### 2.3 NATS JetStream  ·  guide 02 §6, 09 "Problem bundles"

Add `NATS.Net` (API + Worker). Register one `NatsClient` singleton per process. `Messaging/JetStreamSetup`
(hosted, idempotent at start): stream `JOBS` (`jobs.>`, work-queue, 1 h max age), stream `RESULTS`
(`results.>`, work-queue), object store `problem-bundles`. Verify method names against current NATS.Net docs.

### 2.4 Problem bundles  ·  guide 09

`Messaging/ProblemBundlePublisher`: on publish (and in the seeder), serialise `{ signature, tests (incl.
hidden), schema, seedSql, model, planRules, metrics, limits }` to `{slug}/v{version}` — never overwrite a
key *[MQ-06]*. Run `--seed` again for the already-seeded problems or add a one-off "publish missing bundles"
step.

### 2.5 Outbox publisher  ·  guide 02 §7, ADR 0003

`/add-background-job` → `Messaging/OutboxPublisher`: every 500 ms, in one transaction,
`SELECT … FOR UPDATE SKIP LOCKED LIMIT 50`, publish with `MsgId = msg.Id` *[MQ-02, MQ-04]*, set
`ProcessedAt` from `IDateTimeProvider` *[DM-04]* or store `Error`.

### 2.6 Results consumer  ·  guide 02 §7

`Messaging/GradeResultConsumer`: durable `api-results` on `RESULTS`; load submission → already final? ack
*[MQ-03]* → `MarkGraded` + `SubmissionTestResult` rows + `UserProblemProgress` upsert → save → remove
`progress:{userId}` → ack.

### 2.7 Sandbox.Runner harness  ·  guide 03 "The grader harness", "Algorithms grader"; 09 "Data-driven test format"

Add `Microsoft.CodeAnalysis.CSharp`. Folders: `Harness/` (stdin reader, nonce writer), `Compilation/`
(Roslyn + reference allow-list + collectible ALC), `Tests/` (arg binding via `System.Text.Json`, compare
modes `exact`/`unordered`/`unorderedNested`/`approx`, generators `randomInts`/`randomStrings`/
`randomIntervals`/`sortedInts` with fixed seeds, `$ref: reference`), `Graders/Algorithms/` (median-of-7
measurement, budgets). Flow: read bundle + code from stdin (reject > 64 KB) → compile → load → run visible
then hidden → print JSON between nonce markers *[SB-04, SB-05]*. Unit-test compare modes and generators
(same seed → same output).

### 2.8 Algorithms sandbox image  ·  guide 03 "Sandbox images"

`sandbox-images/algorithms.Dockerfile`: SDK stage publishes `src/Sandbox.Runner` (+ `src/Contracts`);
runtime stage, `useradd --uid 10001`, `USER 10001`, `WORKDIR /tmp`,
`ENTRYPOINT ["dotnet", "/opt/runner/CodeBench.Sandbox.Runner.dll", "--grader", "algorithms"]`.
No problems copied in — bundles arrive on stdin (ADR 0005). Load it into the nested daemon for dev:

```powershell
docker build -f sandbox-images/algorithms.Dockerfile -t codebench-sandbox-algorithms:dev .
docker save codebench-sandbox-algorithms:dev | docker compose -f deploy/docker-compose.yml exec -T sandbox-daemon docker load
```

### 2.9 Worker  ·  guide 03 "The worker consumer", "Starting a sandbox"

Add `Docker.DotNet`. In `src/Worker`: `SandboxOptions` (Images, Runtime, MaxParallel, Registry),
`BundleCache` (object store reads, cached by key), `SandboxRunner` (all hardening flags *[SB-01]*, attach
stdin → write bundle + code → start → wait with `CancelAfter` → kill on timeout → read ≤ 1 MB stdout →
parse nonce block → `finally` force-remove *[SB-03]*), `SubmissionConsumer` (durable `graders`,
`MaxAckPending = MaxParallel`, `MaxDeliver = 3`, publish `GradeCompleted` with
`MsgId = graded-{id}`, then ack), `SandboxJanitor` (every minute, remove labelled containers > 5 min),
image pull at start-up. Its own `IDateTimeProvider`. No DB anything *[AR-02]*.

### 2.10 Hostile-submission suite  ·  guide 03 checklist, 06

`tests/hostile-submissions/<case>/{Solution.cs,expected.json}` for: `HttpClient` to the internet,
`while(true)`, 2 GB allocation, process spawning, writes to `/etc` and `/opt`, fake result JSON, correct
O(n) solution. `scripts/test-sandbox.sh` runs each with the worker's exact flags.

### 2.11 UI  ·  guide 04 "Run, Submit and polling", "Editor page layout"

`useSubmitCode('runs' | 'submissions')` with `Idempotency-Key` *[FE-07]*, `useSubmissionResult` polling
750 ms until Graded/Failed *[FE-03]*; Run/Submit buttons; results panel (name, pass/fail, µs, bytes,
budget bars, `aria-live`) *[FE-10]*; compiler diagnostics → Monaco markers; signed-out Submit → sign-in →
resubmit from `sessionStorage`.

### 2.12 Tests

Integration (Testcontainers Postgres **and** NATS `nats:2 -js`): `POST /submissions` writes submission +
outbox row; outbox publisher puts a message on `jobs.submissions.created`; a fake `GradeCompleted` on
`results.graded` grades the submission and updates progress; third in-flight submission → 400
`Submissions.TooManyInFlight`; anonymous `POST /runs` on a non-free problem → 401/404; another user's
submission → 404. Arch: Worker still DB-free.

> **Verify:** with `--profile app --profile grading` up and the image loaded: run `sum-of-evens` with the
> reference solution → Accepted with per-test µs and bytes; a LINQ solution fails the 1 KB allocation
> budget; `while(true)` → Timeout; hostile suite passes; `docker compose … exec sandbox-daemon docker ps -a`
> shows no leftover containers.

---

## Phase 3 — CI/CD  ·  guide 06

**Goal:** a merge to `main` produces versioned images; `update.sh` deploys them locally.

### 3.1 `ci.yml`
Already present (dotnet job). Ensure the frontend matrix job (web, marketing) runs lint, test, build, and
add a step `powershell -File scripts/check-rules.ps1` (use `pwsh` on `ubuntu-latest`).

### 3.2 `images.yml`
Matrix `api`, `worker`, `web`, `marketing` → `ghcr.io/<owner>/codebench-<name>:<sha>` + `:latest`, GHA
cache, lowercase owner, gated on green CI (`workflow_run` or branch protection).

### 3.3 `sandbox-images.yml`
On changes to `src/Sandbox.Runner/**`, `src/Contracts/**`, `sandbox-images/**`: build with `load: true`,
run `scripts/test-sandbox.sh <image> tests/hostile-submissions`, push only on `main`.

### 3.4 `e2e.yml`
Nightly + manual: `deploy/.env.ci` (committed, no secrets) + `CLERK_TEST_PUBLISHABLE_KEY` secret, `compose up
--wait`, Playwright smoke (anonymous: open free problem → Run → result row), upload report + compose logs.

### 3.5 Repository settings
Branch protection on `main` (required checks, PR review), Dependabot (nuget, npm, docker,
github-actions — already partly configured), secret scanning + push protection, pin third-party actions.

### 3.6 Deploy & rollback
`deploy/update.sh` (and a `update.ps1` twin for Windows): `pull` → `run --rm api --migrate` → `up -d
--remove-orphans` → `image prune`. `TAG=<sha>` pins/rolls back. GHCR login with a `read:packages` PAT.

> **Verify:** open a PR → CI green; merge → four images in GHCR tagged by SHA; `./deploy/update.sh` brings
> them up; `./deploy/update.sh <previous-sha>` rolls back and the app still works.

---

## Phase 3A — Admin Console  ·  guide 09 "Admin API", "Admin UI"

**Goal:** an admin can create a problem, add tests, validate it against the reference, publish it, and a
learner can solve it — with no image rebuild.

### 3A.1 Admin route group
`app.MapGroup("admin").RequireAuthorization().HasPermission(Permissions.AdminAccess).WithTags(Tags.Admin)`;
admin slices live in `Features/Admin/` and add their extra permission per endpoint *[EP-04]*.

### 3A.2 Problems
`ListProblems`, `GetProblemForEdit` (incl. hidden tests + reference — admin only), `CreateProblem` (Draft),
`UpdateProblem` (`If-Match` → 409 on stale `xmin` *[DM-07]*; editing a Published problem edits a draft
copy *[DM-08]*), `DeleteProblem` (Drafts only), `ArchiveProblem`.

### 3A.3 Tests
`CreateTestCase`, `UpdateTestCase`, `DeleteTestCase`, `ReorderTestCases`.

### 3A.4 Validate → Publish
`ValidateProblem` creates a hidden system submission running the **reference solution** through the real
worker against all tests and returns measured time/allocations (set budgets ≈ 2–3× the median).
`PublishProblem` refuses unless the latest validation of the current draft passed (409
`Problems.NotValidated`), then increments `Version`, writes `{slug}/v{n}`, sets Published, invalidates
`problems:*`, audits.

### 3A.5 Import / export
`ImportProblemYaml` / `ExportProblemYaml` (same YAML shape as `problems/`).

### 3A.6 Users, submissions, audit, stats
`ListUsers`, `ChangeUserRole` (invalidates `permissions:*`), `DisableUser`; `ListAllSubmissions`,
`GetAnySubmission`, `RegradeSubmission`; `AuditLog` entity + `SaveChangesInterceptor` recording
`Problem`, `TestCase`, `User.Role` changes with before/after JSON; `ListAuditLog`; `GetAdminStats`
(SQL, cached 60 s, `queue.pendingJobs` from consumer info).

### 3A.7 Demo data
`DemoDataSeeder` (`--seed-demo`, Development only): `demo-ana`, `demo-ben`, `demo-chen` with realistic
submissions *[SC-05]*.

### 3A.8 Admin UI
Lazy `/admin` behind `RequirePermission permission="admin:access"` *[FE-08]*; add `@tanstack/react-table`,
`react-hook-form`, `@hookform/resolvers`, `recharts`, `react-markdown`. Pages: Dashboard, Problems table,
Problem editor (Details · Statement · Code · Tests grid · Rules · Preview; footer Save draft · Validate ·
Publish), Users, Submissions, Audit log. Admin link in header when `/me` has `admin:access`.

> **Verify:** guide 09 test list passes — learner 403 on every `/admin/*`; author edits but can't publish;
> admin publishes; publish without validation → 409; stale `If-Match` → 409; `GET /problems/{slug}` never
> contains hidden tests/reference; publish writes `{slug}/v{n}`; E2E: admin creates → validates → publishes,
> learner solves.

---

## Phase 4 — Observability & Hardening  ·  guide 03 checklist, 05, 07 §4

### 4.1 Trace context through NATS *[MQ-07]*
Inject W3C `traceparent` into NATS headers on publish (outbox, worker), extract on consume; add an
`ActivitySource` per process. One trace spans API → NATS → worker → result.

### 4.2 Dashboards & alerts
Grafana: submissions/min, grading latency p50/p95, verdict mix, `JOBS` queue depth, sandbox failures,
DinD volume disk usage (alert). Decide whether to split `otel-lgtm` into separate services (KD-04).

### 4.3 Sandbox hardening
Janitor verified; on Linux/WSL2 native engine try gVisor (`runsc`) and compare timings; Redis/NATS
passwords on; hardening checklist from guide 03 fully ticked.

### 4.4 REA audits in CI
Web bundle audit (no secrets, no source maps, no unprotected routes) and worker assembly audit (no
Npgsql/EF, config keys listed) uploaded as artifacts.

### 4.5 Backups
`--profile backup` (`db-backup`); restore one backup into a scratch database end to end.

> **Verify:** one trace in Tempo shows API → worker → result for a Run; dashboards populated; a restored
> backup contains your problems.

---

## Phase 5 — Database / EF Core Grader  ·  guide 03 "Database grader", 09 problem 5

### 5.1 Database sandbox image
`sandbox-images/database.Dockerfile` + `db-entrypoint.sh`: Postgres in `/tmp/pgdata` (tmpfs ~256 MB),
Unix socket only, pre-initialised data dir baked in for fast start.

### 5.2 Harness
`Graders/Database/`: run bundle `schema` + `seedSql` + `ANALYZE`; build the problem's `DbContext` from
`model`; `DbCommandInterceptor` captures SQL + parameters; invoke user method; compare rows;
`EXPLAIN (FORMAT JSON)` each statement.

### 5.3 Plan rules
`noSeqScan`, `usesIndex`, `maxRoundTrips` as walks over the JSON plan; results in `Details`.

### 5.4 UI
Expandable SQL + plan summary + rule outcomes per test.

### 5.5 Problems
Seed `problems/customers-since` (check the plan rule against the reference on the seeded volume first),
then write four more originals: N+1, filter-before-fetch, sargable date ranges, projection, pagination.

> **Verify:** `customers-since` reference → Accepted; an in-memory filter fails `one-trip`; an N+1 loop
> fails `one-trip`; SQL and plan visible in the UI.

---

## Phase 6 — More Graders  ·  guide 03 "Other graders", 09 problem 6

| Step | Grader | Mechanism |
|---|---|---|
| 6.1 | Refactoring | behaviour tests + Roslyn `CSharpSyntaxWalker` metrics (cyclomatic complexity, nesting, method length, duplication) vs per-problem thresholds; seed `shipping-cost-refactor` |
| 6.2 | Architecture | multi-file submissions compiled into separate assemblies; NetArchTest rules |
| 6.3 | Secure coding | normal tests + adversarial suite (path traversal, SQL injection payloads) |
| 6.4 | Test writing | user's tests vs correct implementation (must pass) and planted mutants (must kill enough) |

Each grader: harness code, image (or flag on an existing image), hostile-suite cases, at least one original
problem, UI for its `Details`.

> **Verify:** one problem per grader goes Accepted with the reference and fails with a known-bad solution.

---

## Phase 7 — Product Polish  ·  guide 07 §7, 02 §5

### 7.1 Progress
`Progress/GetProgress` (`GET /me/progress`): solved, attempts, streaks, XP from `UserProblemProgress`,
cached `progress:{userId}`; catalog checkmarks.

### 7.2 Learning paths
Ordered problem lists with prerequisites (entity + admin CRUD + learner view).

### 7.3 Plans & permissions
Free vs Pro tracks mapped to permissions; `GetProblem` enforces.

### 7.4 Payments (sandbox only)
Choose Paddle **or** Stripe test mode; checkout from the web app (public token only), signed webhook slice
changes `User.Plan`. Not tax advice — check before real money.

### 7.5 Optional public preview
Cloudflare Tunnel + Clerk production instance on your own domain.

> **Verify:** a sandbox payment flips a user to Pro and unlocks a Pro track; progress and streaks render.

---

## Appendix A — The repeatable feature loop

Every new use case follows the same order. Example: *"learners can bookmark problems"*.

```
1. ENTITY      Features/Bookmarks/Bookmark.cs (+ BookmarkErrors, domain event if needed)   /add-entity
2. CONFIG      Database/Configurations/BookmarkConfiguration.cs (unique user_id+problem_id)
3. MIGRATION   /add-migration Add_Bookmarks
4. SLICE       Features/Bookmarks/AddBookmark.cs — Command, Validator, Handler, Endpoint   /add-feature
5. CACHE       BookmarkCacheKeys; invalidate in the command                                [QY-05]
6. TESTS       unit (handler failure paths + happy path), validator, integration          [TS-01]
7. CONTRACT    docs/api-contract.md row
8. FRONTEND    features/bookmarks/useBookmarks.ts (TanStack Query) + component             [FE-03]
9. VERIFY      dotnet test · check-rules.ps1 · npm test · /vsa-review
```

Zero edits to `Program.cs` / `DependencyInjection.cs` *[VS-03]*.

## Appendix B — Daily run

```powershell
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d
dotnet watch --project src/Web.Api            # http://localhost:5000, Scalar at /scalar
dotnet watch --project src/Worker             # needs DOCKER_HOST=tcp://127.0.0.1:2375 (--profile grading)
npm --prefix apps/web run dev                 # http://localhost:5173
npm --prefix apps/marketing run dev           # http://localhost:4321
```

Grafana http://localhost:3000 · JetStream: `docker run --rm --network codebench_backend natsio/nats-box nats -s nats://codebench:<pw>@nats:4222 stream ls`.

## Appendix C — Troubleshooting  ·  guide 05

| Symptom | Likely cause |
|---|---|
| 404 from Traefik | router label typo / container not on `edge` |
| CORS error | origin missing from `Cors:AllowedOrigins` |
| 401 everywhere | wrong `Clerk:Issuer` or `azp` not in `AuthorizedParties` |
| Runs stay Pending | worker can't reach NATS or sandbox daemon; image not loaded in DinD; outbox `error` column |
| Integration tests time out | Docker Desktop not running |
| Noisy timings | power saving / load — median of 7, budgets with headroom, `DOTNET_TieredPGO=0` |
