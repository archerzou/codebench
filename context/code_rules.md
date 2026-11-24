# CodeBench — Vertical Slice & Frontend Rules & Review Rubric

**Purpose:** an enforceable rulebook for this codebase. Feed it to Claude Code (or any review tool) to
audit changes, and use it yourself before opening a PR.

**Version:** 1.1 · **Last updated:** 2026-11-19 · **Applies to:** CodeBench (.NET 10 API + Worker +
Sandbox.Runner, React 19 + Vite + TS app, Astro site)

**Companions:** `context/build_plan.md` (how to build it) · `context/progress_tracker.md` (where you are) ·
`CLAUDE.md` (short form of these rules) · `docs/guide/` (the design) · `docs/adr/` (why)

**Precedence:** this file > code snippets in `docs/guide/`. The guide's snippets are illustrations; where
one conflicts with a rule here (e.g. logic inside an endpoint lambda, `DateTimeOffset.UtcNow`), the rule wins.

---

## How to use this document

### With Claude Code

```
Review the current diff against context/code_rules.md.
For each violation report: rule ID, file:line, severity, why it matters, and the fix.
Do not report style opinions that aren't in the rulebook.
```

Scoped variants:

```
Audit src/Web.Api/Features/Submissions against rules VS-*, EP-* and QY-*. Violations only.
Check the security boundaries (AR-02..AR-04, SB-*, SC-*) across the whole repo.
I'm adding CreateSubmission. Which rules apply, in what order?
Audit apps/web/src against FE-*.
```

The `/vsa-review` skill covers the VS/EP/DM/QY/TS subset; this file is the full rubric.

### Severity levels

| Level | Meaning | Action |
|---|---|---|
| 🔴 **BLOCKER** | Breaks the architecture or a security boundary. | Must fix before merge. |
| 🟠 **MAJOR** | Correctness, security, or performance defect. | Fix before merge, or log a dated item in the tracker. |
| 🟡 **MINOR** | Convention drift. Compounds if ignored. | Fix in this PR if cheap. |
| 🔵 **INFO** | Observation or trade-off worth recording. | Note in the PR description. |

### Scoring

```
Score = 100 − (BLOCKER × 25) − (MAJOR × 10) − (MINOR × 3)

≥ 90  Ship it
70–89 Fix majors, then ship
50–69 Needs rework
< 50  Architectural review required
```

---

# Part 1 — Architecture & Boundary Rules (AR)

There are no layers. There are **deployables with boundaries**, and inside the API there are **slices**.

```
 ┌──────────── edge ────────────┐   ┌──────────── backend ─────────────┐   ┌──── exec (internal) ────┐
 │ apps/web   apps/marketing    │   │ Postgres  Redis  NATS  observ.   │   │ sandbox-daemon (DinD)   │
 │        │                     │   │    ▲                ▲   ▲        │   │    └─ sandbox (no net)  │
 │        ▼                     │   │    │ EF             │   │ NATS   │   │          ▲ stdin        │
 │   src/Web.Api ───────────────┼──►│────┘     outbox ────┘   │        │   │          │              │
 │   (Features/* slices)        │   │                          │        │   │          │              │
 └──────────────────────────────┘   │   src/Worker ────────────┘────────┼──►│ Docker API ─────────────┘
                                    └──────────────────────────────────┘   └─────────────────────────┘
   src/Contracts  ← referenced by Web.Api, Worker, Sandbox.Runner; references nothing
```

### AR-01 🔴 One API project; no layer projects

All API code lives in `src/Web.Api` (namespace `Web.Api`). No `Domain` / `Application` /
`Infrastructure` / `SharedKernel` projects, no `IApplicationDbContext`, no MediatR.

**Why:** the template's whole bet is that a use case reads top to bottom in one file. Reintroducing
layers spreads one change across four projects.
**Check:** `check-rules.ps1` AR-01, VS-03.

---

### AR-02 🔴 The Worker has no database access

`src/Worker` must not reference EF Core, Npgsql or `Web.Api`, must not read a `Database` connection
string, and its environment contains no database, Clerk or payment values.

```powershell
dotnet test tests/ArchitectureTests --filter BoundaryTests     # compiled-reference check
powershell -File scripts/check-rules.ps1                       # source/config check
```

**Why:** the worker drives the daemon that runs untrusted code. If a sandbox escape lands on the
worker, there must be nothing there worth stealing (guide 03, threat model). Phase 4 adds a REA audit
of the published `CodeBench.Worker.dll` (guide 01).

---

### AR-03 🔴 `src/Contracts` depends on the BCL only

No `PackageReference`, no `ProjectReference`. Records only; no behaviour beyond trivial factories.

**Why:** both sides of the queue compile against it. A dependency here becomes a dependency of the
worker, which reopens AR-02 through the back door.

---

### AR-04 🔴 `Sandbox.Runner` references only Contracts and grader libraries

Allowed: `CodeBench.Contracts`, `Microsoft.CodeAnalysis.CSharp`, and (database grader only) EF Core +
Npgsql against the **in-sandbox** Postgres. Never `Web.Api`, never `Worker`.

---

### AR-05 🔴 No cross-feature slice coupling

A slice may use `Common`, `Database`, `Messaging`, `Authentication`, `Authorization` and another
feature's **domain types** (entity, `{Entity}Errors`, domain events, `{Feature}CacheKeys`). It must
**never** use another feature's `Command` / `Query` / `Handler` / `Validator` / `Endpoint` / `Response`.

```csharp
// ❌ Submissions reaching into the Problems slice
var detail = await getProblemHandler.Handle(new GetProblem.Query(slug), ct);

// ✅ Query the shared data directly, project what this slice needs
var problem = await db.Problems.Where(p => p.Id == cmd.ProblemId)
    .Select(p => new { p.Slug, p.Version, p.GraderKind, p.Limits }).SingleOrDefaultAsync(ct);
```

**Why:** slices that call slices recreate a service layer, and deleting a feature stops being
"delete a folder". Duplication of a 3-line query is cheaper than coupling.
**Check:** `check-rules.ps1` AR-05.

---

### AR-06 🔴 The API never references the Worker or the Runner

They communicate only through NATS and the records in `src/Contracts`.

---

### AR-07 🟠 Infrastructure that isn't a use case lives outside `Features/`

| Concern | Folder |
|---|---|
| Outbox publisher, results consumer, JetStream setup, bundle publisher | `Web.Api/Messaging/` |
| Seeders (`ProblemSeeder`, `RoleSeeder`, `DemoDataSeeder`) | `Web.Api/Database/Seeding/` |
| Periodic jobs that aren't messaging | `Web.Api/BackgroundJobs/` |
| Clerk JWT, `IUserContext`, user upsert middleware | `Web.Api/Authentication/` |
| Permissions, role → permission loading | `Web.Api/Authorization/` |

`Common/Messaging` is the in-process CQRS interfaces. Do not put NATS code there.

---

### AR-08 🟡 Shared constants over magic strings

| Thing | Single source |
|---|---|
| NATS subjects | `CodeBench.Contracts.Subjects` |
| Verdicts | `CodeBench.Contracts.Verdicts` |
| OpenAPI tags | `Web.Api.Common.Endpoints.Tags` |
| Permission names | `Web.Api.Authorization.Permissions` (one catalog, `"area:action"`); grants in `RolePermissionMatrix` |
| Rate-limit policies | `Web.Api.Common.RateLimitingPolicies` |
| Cache keys | `{Feature}CacheKeys` |

**Check:** `check-rules.ps1` AR-08 (subjects). Review the rest manually.

---

# Part 2 — Vertical Slice Rules (VS)

### VS-01 🔴 One use case = one file = one static class

`src/Web.Api/Features/{Feature}/{UseCase}.cs`, namespace `Web.Api.Features.{Feature}`, containing
`public static class {UseCase}` that nests the **whole** slice. No separate `Endpoints/`, `Handlers/`,
`Validators/` or `Dtos/` folders.

```csharp
public static class GetProblem
{
    public sealed record Query(string Slug) : IQuery<Response>;
    public sealed record Response { /* ... */ }
    internal sealed class Handler(ApplicationDbContext db, HybridCache cache) : IQueryHandler<Query, Response> { /* ... */ }
    public sealed class Endpoint : IEndpoint { /* ... */ }
}
```

**Why:** everything for one behaviour is co-located; deleting a feature is deleting files in one folder.

---

### VS-02 🔴 Nested type shapes are fixed

| Type | Shape |
|---|---|
| `Command` / `Query` | `public sealed record`, positional, immutable; implements `ICommand`, `ICommand<T>` or `IQuery<T>` |
| `Validator` | `public sealed class Validator : AbstractValidator<Command>` |
| `Handler` | `internal sealed class Handler(...)` with a **primary constructor** |
| `Response` | `public sealed record Response` (nested DTOs also nested here) |
| `Endpoint` | `public sealed class Endpoint : IEndpoint`; an HTTP body that differs from the command is a nested `Request` record |
| Domain event handlers for this use case | `internal sealed class XDomainEventHandler : IDomainEventHandler<XDomainEvent>` |

---

### VS-03 🔴 Never wire a slice by hand

Handlers are found by Scrutor, validators by `AddValidatorsFromAssembly(includeInternalTypes: true)`,
endpoints by `AddEndpoints`, decorators applied with `TryDecorate`. A slice needs **zero** edits to
`Program.cs` or `DependencyInjection.cs`.

---

### VS-04 🔴 Handlers contain only use-case logic

| Anti-pattern in a handler | Where it belongs |
|---|---|
| Input-shape checks (`IsNullOrEmpty`, length, range) | the nested `Validator` |
| `try { } catch { }` around business rules | return `Result.Failure`; unexpected errors → `GlobalExceptionHandler` |
| `logger.LogInformation("Handling …")` | `LoggingDecorator` |
| `Stopwatch`, timing | OpenTelemetry |
| Reading `HttpContext` | `IUserContext` |
| `DateTime.UtcNow` | `IDateTimeProvider` (DM-04) |

Business rules **do** belong in the handler or the entity: "≤ 2 submissions in flight", "problem must be
Published", "only Drafts can be deleted".

**Benchmark:** handlers are typically 10–30 lines. Over ~40 means something cross-cutting leaked in, or
the entity should own more of the rule.

---

### VS-05 🔴 Every command has a validator; queries have none

The validator runs in `ValidationDecorator` (commands only). A query that needs input rules expresses them
in its route (`{id:guid}`) or clamps them in the handler (e.g. `pageSize = Math.Clamp(pageSize, 1, 100)`).
**Check:** `check-rules.ps1` VS-05.

---

### VS-06 🔴 `CancellationToken` threaded through every async call

Endpoint → handler → every EF/NATS/HybridCache/Docker call. A dropped token means work continues after
the client (or the host) is gone.

---

### VS-07 🟠 Commands carry intent, not identity

Never put the caller's identity in the request body. The user comes from `IUserContext` in the handler;
the route provides resource ids.

```csharp
// ❌ trusts the client
public sealed record Command(Guid UserId, Guid ProblemId, string Code) : ICommand<Guid>;
// ✅
public sealed record Command(Guid ProblemId, string Code) : ICommand<Guid>;
```

---

# Part 3 — Domain & Data Rules (DM)

### DM-01 🔴 Entities derive from `Entity`; domain events raised with `Raise`

Raise events inside entity factories/methods (`Submission.Create`, `submission.MarkGraded(...)`) so every
caller gets them; raising in a handler before `SaveChangesAsync` is the accepted template fallback.
Events dispatch **after** the save (`ApplicationDbContext.SaveChangesAsync`) — use them for in-process
side effects only. Anything that must reach another process goes through the outbox (MQ-01).

---

### DM-02 🔴 State transitions are methods with guards

`Submission.Status`: `Pending → Running → Graded | Failed`. `Problem.Status`: `Draft → Published → Archived`.
Transitions are entity methods that refuse illegal moves and are **idempotent** (grading the same
submission twice is a no-op).

```csharp
public Result MarkGraded(string verdict, DateTime gradedAt)
{
    if (Status == SubmissionStatus.Graded) return Result.Success();          // idempotent
    if (Status == SubmissionStatus.Failed) return Result.Failure(SubmissionErrors.AlreadyFinal);
    Status = SubmissionStatus.Graded; Verdict = verdict; GradedAt = gradedAt;
    Raise(new SubmissionGradedDomainEvent(Id, UserId, ProblemId, verdict));
    return Result.Success();
}
```

---

### DM-03 🔴 Persistence lives in `Database/Configurations/`

One `IEntityTypeConfiguration<T>` per entity, auto-applied. Keys, lengths, indexes, conversions, jsonb
columns, `xmin` concurrency, unique constraints — all here. Entities use **FK ids**, no navigation
properties. snake_case is applied globally; don't hand-name columns.

Required indexes (guide 02 §3): `users (external_id)` unique, `problems (slug)` unique,
`submissions (user_id, created_at DESC)`, partial `outbox_messages (processed_at) WHERE processed_at IS NULL`.

---

### DM-04 🟠 Time comes from `IDateTimeProvider`

Never `DateTime.UtcNow`, `DateTime.Now` or `DateTimeOffset.UtcNow` outside `DateTimeProvider.cs` — in
handlers, entities, background services or the worker (the worker registers its own provider).
Store UTC (`timestamptz`). Guide snippets that inject `TimeProvider clock` translate to `IDateTimeProvider`.
**Check:** `check-rules.ps1` DM-04.

---

### DM-05 🟠 Entities are persistence-ignorant

No `[Table]`, `[Column]`, `[Key]`, `[MaxLength]`, `[Required]`, `[ForeignKey]` on entities.
**Check:** `check-rules.ps1` DM-05.

---

### DM-06 🟠 Migrations through the tooling only

Create with `/add-migration` (`dotnet ef migrations add … --project src/Web.Api`). Never edit a migration
that has been applied anywhere; add a new one. Migrations are `generated_code` in `.editorconfig` — don't
restyle them. Production applies migrations with the explicit `--migrate` run, not on startup.

---

### DM-07 🟠 Optimistic concurrency where humans edit

`Problem` uses Postgres `xmin` as a concurrency token; `UpdateProblem` takes `If-Match` and returns 409
(`ErrorType.Conflict`) on mismatch (guide 09).

---

### DM-08 🟠 Published content is immutable

A Published problem version never changes in place. Edits go to a Draft; Publish increments `Version` and
writes a new bundle. Submissions record `ProblemVersion`.

---

# Part 4 — Query & Caching Rules (QY)

### QY-01 🔴 Queries project to the nested `Response`

```csharp
// ✅ SQL selects only what the response needs
Response? problem = await db.Problems
    .Where(p => p.Slug == query.Slug && p.Status == ProblemStatus.Published)
    .Select(p => new Response { /* ... */ })
    .SingleOrDefaultAsync(ct);

// ❌ loads the entity (incl. ReferenceSolution) and maps in memory
var problem = await db.Problems.SingleAsync(p => p.Slug == query.Slug, ct);
return new Response(problem.Title, ...);
```

Never return an entity from an endpoint. Writes may load tracked entities; reads use projection
(or `AsNoTracking()` when an entity load is unavoidable).

---

### QY-02 🟠 No client-side evaluation

No `.ToList()` / `.AsEnumerable()` before `Where` / `OrderBy` / `Skip` / `Take`.

---

### QY-03 🟠 List endpoints return `PagedList<T>` with a capped page size

Search + filter + sort + page, `pageSize` clamped (≤ 100). Sort keys are an allow-list, never a raw
column name from the query string.

---

### QY-04 🔴 Hidden tests and reference solutions never leave the server on public routes

Public `Response` types have **no** property that could hold hidden test args/expected values or the
reference solution. Hidden results expose `Name`, `Passed`, timings and a generic message only.
Admin routes (`/admin/*`) are the only exception.
**Check:** integration test asserting on the raw JSON of `GET /problems/{slug}` and `GET /submissions/{id}`.

---

### QY-05 🟠 HybridCache keys from `{Feature}CacheKeys`; every mutating command invalidates

| Key | Owner | Invalidated by |
|---|---|---|
| `problems:list:{hash}` / `problems:{slug}` (10 min) | Problems | seeding, Publish, Archive, Update |
| `users:ext:{sub}` | Users | webhook update/delete, role change |
| `permissions:{externalId}` (5 min, tag `permissions`) | Authorization | `ChangeUserRole`, `DisableUser` (key); `RoleSeeder` when the matrix changes (tag) |
| `progress:{userId}` | Progress | results consumer |
| `admin:stats:{days}` (60 s) | Admin | expiry only |

A cache read without matching invalidation is a stale-data bug waiting to happen.

---

# Part 5 — Errors & Endpoint Rules (EP)

### EP-01 🔴 Expected failures return `Result`, never throw

`Result.Failure(...)` / `Result.Failure<T>(...)` with an `Error`. `throw` is for bugs (invariant
violations), which `GlobalExceptionHandler` turns into 500.
**Check:** `check-rules.ps1` EP-01.

---

### EP-02 🟠 Errors come from `{Entity}Errors` with the right `ErrorType`

Codes are `"{FeaturePlural}.{Reason}"`: `Problems.NotFound`, `Submissions.TooManyInFlight`,
`Problems.NotValidated`, `Problems.VersionMismatch`.

| ErrorType | Status | Use for |
|---|---|---|
| `Validation` | 400 | produced by `ValidationDecorator` only |
| `Problem` | 400 | business rule rejected the request |
| `NotFound` | 404 | resource missing **or not visible to the caller** (don't leak existence) |
| `Conflict` | 409 | concurrency, duplicate, illegal state transition, publish without validation |
| `Failure` | 500 | unexpected failure surfaced as a result |

---

### EP-03 🔴 Endpoints are thin

Inside `MapX(...)`: bind route/body → build the command/query → `await handler.Handle(...)` →
`result.Match(...)`. No `ApplicationDbContext`, no business `if`, no try/catch.

```csharp
app.MapPost("submissions", async (Request request, ICommandHandler<Command, Guid> handler, CancellationToken ct) =>
    (await handler.Handle(new Command(request.ProblemId, request.Code), ct))
        .Match(id => Results.Accepted($"/submissions/{id}", new { id }), CustomResults.Problem))
```

**Exception — webhooks:** signature verification (Svix) is an HTTP concern and happens in the endpoint,
**before** deserialising; the verified event is then handed to a command handler. The guide 02 snippet that
writes to the DB inside the endpoint is an illustration, not the pattern.
**Check:** `check-rules.ps1` EP-03.

---

### EP-04 🔴 Every endpoint makes an explicit auth decision

Exactly one of `.RequireAuthorization()`, `.HasPermission(Permissions.X)`, or `.AllowAnonymous()`
(anonymous is a deliberate choice, visible in review). Admin endpoints map inside the `/admin` group that
requires `admin:access`, then add their extra permission per endpoint.

| Route | Decision |
|---|---|
| `GET /problems`, `GET /problems/{slug}` | `AllowAnonymous` (Pro tracks checked in handler) |
| `POST /runs` | `AllowAnonymous` for free problems + `anonymous-runs` rate limit |
| `POST /webhooks/*` | `AllowAnonymous` + signature + `ExcludeFromDescription()` |
| everything else | `RequireAuthorization` / `HasPermission` |

**Check:** `check-rules.ps1` EP-04.

---

### EP-05 🟡 Tagged with `Tags` constants

`.WithTags(Tags.Problems)` etc. **Check:** `check-rules.ps1` EP-05.

---

### EP-06 🟠 Work-creating writes are idempotent, rate-limited, and return 202

`POST /runs` and `POST /submissions`: `.WithIdempotency()`, `.RequireRateLimiting(...)`
(`submissions` per user, `anonymous-runs` per IP), and `Results.Accepted("/submissions/{id}", new { id })`.

---

### EP-07 🔴 Ownership is enforced in the handler

User-owned reads (`GET /submissions/{id}`, `/me/*`) filter by the current user's id. A foreign id returns
`NotFound`, not `Forbidden`.

---

# Part 6 — Messaging & Grading Pipeline Rules (MQ)

### MQ-01 🔴 Submissions are enqueued only through the outbox

The `Submission` and its `OutboxMessage` are added in the same `SaveChangesAsync`. No handler or endpoint
publishes to NATS directly (ADR 0003).

---

### MQ-02 🔴 Every publish carries a dedupe `MsgId`

Outbox: `MsgId = outbox id`. Worker results: `MsgId = $"graded-{submissionId}"`.

---

### MQ-03 🔴 Consumers are idempotent and ack last

Process → persist / publish → **then** ack. A redelivered message must be harmless (already `Graded` → ack
and return). Poison messages stop at `MaxDeliver` and are logged with the submission id.

---

### MQ-04 🟠 Outbox claims rows with `FOR UPDATE SKIP LOCKED` inside one transaction

So two API instances never publish the same row; the publish error is stored on the row, not swallowed.

---

### MQ-05 🟠 Contracts evolve additively

Add fields at the end with defaults; never rename or retype a field in place. A breaking change is a new
record type and subject. Messages carry slug + version, **never** hidden tests (bundles do).

---

### MQ-06 🟠 Problem bundles are immutable and versioned

Object store `problem-bundles`, key `{slug}/v{version}`. Never overwrite a key; the worker caches by key.

---

### MQ-07 🟡 Trace context crosses the queue (Phase 4)

W3C `traceparent` goes into NATS headers on publish and is restored on consume, so one trace spans
API → worker → result.

---

# Part 7 — Sandbox Security Rules (SB)

### SB-01 🔴 Every sandbox container is fully hardened

`NetworkMode = "none"` (+ `NetworkDisabled`), `ReadonlyRootfs = true`, size-capped `noexec,nosuid` `/tmp`
tmpfs, `CapDrop = ["ALL"]`, `SecurityOpt = ["no-new-privileges"]`, `User = "10001:10001"`, `PidsLimit`,
`Memory == MemorySwap`, `NanoCPUs`, `Runtime` from config (`runsc` where available). Guide 03.
**Check:** `check-rules.ps1` SB-01 (activates when `SandboxRunner.cs` exists).

---

### SB-02 🔴 The host Docker socket is never used

The worker talks to `tcp://sandbox-daemon:2375` on the internal `exec` network. `/var/run/docker.sock` is
mounted only into Traefik (read-only, labels). ADR 0002.

---

### SB-03 🔴 The worker enforces the wall clock and always removes the container

`CancelAfter(TimeoutSeconds)` → `KillContainerAsync` → `Verdict = Timeout`; `RemoveContainerAsync(Force)`
in `finally`. A janitor removes `codebench.submission`-labelled containers older than 5 minutes.

---

### SB-04 🔴 Results are parsed only from the nonce block, size-capped and schema-validated

Stdout capped at 1 MB; JSON taken only between the per-run nonce markers generated before user code
loads; anything that fails validation becomes `InternalError`. User-printed JSON is ignored.

---

### SB-05 🟠 The runner rejects oversize input and loads user code collectibly

Code > 64 KB rejected; compiled with Roslyn against an allow-list of references; loaded into a
**collectible** `AssemblyLoadContext`; tests run single-threaded for meaningful allocation numbers.
The allow-list is convenience, **not** a security boundary — the container is.

---

### SB-06 🔴 The hostile-submission suite passes for every sandbox image

`tests/hostile-submissions/` (network, infinite loop, 2 GB allocation, process spawning, filesystem
writes, fake result JSON, correct solution) runs in CI against each image before it is pushed.

---

# Part 8 — Security & Configuration Rules (SC)

### SC-01 🔴 No secrets in source

Only `*.env.example` files are committed, with secret values empty. Real values: `deploy/.env`
(git-ignored), `dotnet user-secrets`, GitHub Actions secrets. Rotate anything that leaks.
**Check:** `check-rules.ps1` SC-01 + GitHub secret scanning/push protection.

---

### SC-02 🔴 Clerk tokens are validated including `azp`

Issuer from `Clerk:Issuer` (JWKS), lifetime validated, `azp` ∈ `Clerk:AuthorizedParties`, `MapInboundClaims
= false`, `sub` is the identity. Webhooks verified with Svix before any processing.

---

### SC-03 🔴 Authorization data lives in our database

Roles (`learner`/`author`/`admin`) and the role→permission matrix are rows in Postgres; `PermissionProvider`
loads them by `ExternalId`. Clerk `publicMetadata.role` is a UI hint only. `User.IsDisabled` blocks access
in the permission handler. First admin only via `Admin:BootstrapEmails`.

---

### SC-04 🟠 CORS is an explicit origin list

`Cors:AllowedOrigins` = app + marketing origins (+ localhost dev ports in Development). Never `AllowAnyOrigin`.

---

### SC-05 🟠 Dev-only data stays dev-only

`--seed-demo` refuses outside Development; demo users have `IsDemo = true` and `ExternalId = "seed|…"`.

---

### SC-06 🟠 Fail fast on missing configuration

Required settings (`ConnectionStrings:*`, `Clerk:Issuer`) are validated at startup (options validation /
`ValidateOnStart`), not discovered on the first request.

---

# Part 9 — Testing Rules (TS)

### TS-01 🔴 Right test type for the concern

| Testing… | Project | Tools |
|---|---|---|
| Handlers (every `Result.Failure` path + happy path, persisted state, raised events) | `tests/Web.Api.UnitTests/{Feature}/` | `BaseHandlerTest` (EF InMemory), NSubstitute, Shouldly |
| Validators (one test per rule) | `tests/Web.Api.UnitTests/{Feature}/` | `FluentValidation.TestHelper` |
| Endpoints over real HTTP + Postgres (+ NATS) | `tests/IntegrationTests/{Feature}/` | `IntegrationTestWebAppFactory`, Testcontainers |
| Boundaries and conventions | `tests/ArchitectureTests/` | NetArchTest, reflection |
| Sandbox hardening | `tests/hostile-submissions/` | `scripts/test-sandbox.sh` |
| Compare modes, generators, plan rules, metrics | `Sandbox.Runner` unit tests | xUnit |
| Components, hooks | `apps/web` | Vitest, Testing Library, MSW |
| User journeys | `e2e/` | Playwright against the Compose stack |

---

### TS-02 🔴 Names and structure match existing tests

`{Subject}_Should_{Outcome}[_When_{Condition}]` (NM-11), Arrange / Act / Assert, Shouldly assertions, nested types
used directly (`new CreateSubmission.Handler(...)`, `new CreateSubmission.Validator()`).

---

### TS-03 🟠 Security-relevant behaviour has an integration test

Hidden data never in public JSON (QY-04); learner gets 403 on every `/admin/*`; author can edit but not
publish; stale `If-Match` → 409; publish without validation → 409; foreign submission → 404;
`POST /submissions` writes a submission **and** an outbox row; a fake `GradeCompleted` updates progress.

---

### TS-04 🟠 Tests don't depend on Clerk

Integration tests replace Clerk with a test JWT scheme issuing `sub` and `azp`. E2E signed-in flows use
Clerk testing tokens from a separate dev instance.

---

### TS-05 🟠 Tests are independent

No shared mutable statics; unique ids/emails per test (`UniqueEmail()`); integration tests never assume
an empty table.

---

# Part 10 — Frontend Rules (FE)

Applies to `apps/web` (React 19 + Vite + TS) and, where relevant, `apps/marketing` (Astro).
Baseline standards: TypeScript strict, the official React rules (hooks rules, pure render), and
`eslint` `recommended` + `typescript-eslint` `strict` + `react-hooks` + `jsx-a11y`.

### FE-01 🔴 TypeScript strict, no `any`

`"strict": true`, `noUncheckedIndexedAccess`, `noImplicitOverride`. Use `unknown` + narrowing (or a zod
parse) instead of `any`. No `@ts-ignore`; `@ts-expect-error` only with a reason.
**Check:** `check-rules.ps1` FE-01, `npm run lint`, `tsc --noEmit`.

---

### FE-02 🟠 Feature folders mirror the API

```
src/
  main.tsx  routes.tsx  config.ts  index.css
  lib/        api.ts (useApi, ApiError), queryClient.ts, utils.ts (cn)
  components/ui/ shadcn/ui primitives (generated, re-skinned — FE-16, KD-07)
  components/ shared presentational components only (Button, Spinner, Badge, VerdictBadge, BudgetBar)
  stores/     zustand stores for client UI state only (FE-15)
  features/
    problems/     ProblemList, ProblemFilters, useProblems.ts, types.ts
    editor/       EditorPage, CodeEditor, StatementPane
    submissions/  RunButton, SubmitButton, ResultsPanel, useSubmission.ts
    account/      ProgressPage, SettingsPage
    admin/        AdminLayout (lazy), ProblemsTable, ProblemEditor, UsersTable, …
```

A feature may import from `lib/`, `components/` and its own folder. Cross-feature imports go through the
other feature's `index.ts` only. `admin/` is never imported by learner features.

---

### FE-03 🔴 Server state lives in TanStack Query

No `useEffect` + `fetch` + `useState` for API data. Query keys are arrays starting with the resource
(`['problems', filters]`, `['submission', id]`). Mutations invalidate the queries they affect. Polling
uses `refetchInterval` that stops on `Graded`/`Failed` (ADR 0004).

---

### FE-04 🔴 All HTTP goes through `lib/api.ts`

`useApi()` attaches `Authorization: Bearer ${await getToken()}` **per request** (never cache the token),
sets JSON headers, and turns non-2xx responses into `ApiError` parsed from RFC 7807 ProblemDetails
(`title`, `detail`, `status`, `errors`). Components never call `fetch`.
**Check:** `check-rules.ps1` FE-04.

---

### FE-05 🔴 Runtime config via `config.ts`; public values only

`window.__CONFIG__` (written by `docker/40-config.sh` at container start) with `import.meta.env.VITE_*`
fallback, read **only** in `config.ts`. Allowed: `API_URL`, `MARKETING_URL`, `CLERK_PUBLISHABLE_KEY`,
Paddle client token / Stripe publishable key. Never anything named `*SECRET*`, `*PASSWORD*` or a server
`*API_KEY*`. **Check:** `check-rules.ps1` FE-05/FE-06 + REA bundle audit (guide 01).

---

### FE-06 🟠 Validate at the edges with zod

API responses that drive logic and every form are parsed with zod schemas (`react-hook-form` +
`@hookform/resolvers/zod`). Types are `z.infer<>` of the schema — one source of truth per shape.

---

### FE-07 🟠 Submit flow is idempotent and resilient

Each Run/Submit click sends a fresh `Idempotency-Key: crypto.randomUUID()`; buttons disable while a
mutation is pending; signed-out Submit stores the code in `sessionStorage` and resubmits after sign-in;
drafts persist per problem in `localStorage`.

---

### FE-08 🟠 Client guards are UX, not security

`<SignedIn>`/`<RedirectToSignIn/>` and `RequirePermission` (reads `GET /me`) only hide UI; the API
enforces everything. `/admin` is `lazy()`-loaded so learners never download it.

---

### FE-09 🟡 Component conventions

Function components + hooks only; one component per file, `PascalCase.tsx`, named exports (file names: NM-13); hooks are
`useX.ts`; props typed with a `type XProps`; no prop drilling beyond two levels (lift to a hook or context);
no business logic in JSX — compute in hooks. **Exception:** `src/components/ui/` (shadcn/ui, KD-07).

---

### FE-10 🟠 Accessible by default

Semantic elements (`button`, not clickable `div`), labelled form controls, keyboard-reachable editor
actions (Run: Ctrl/Cmd+Enter), `aria-live="polite"` on the results panel, colour never the only signal for
pass/fail (icon + text), focus returned after dialogs.

---

### FE-11 🟡 Styling with Tailwind; no inline style sprawl

Tailwind utilities (v4 via `@tailwindcss/vite`); shared variants in components, not copy-pasted class strings.
Variants are declared with `cva` (class-variance-authority) and merged with `cn()` from `lib/utils.ts`; only
colour **roles** from `tokens.css` (`bg-surface`, `text-ink-muted`, `bg-primary` via the bridge) — never raw
palette classes (`bg-zinc-900`, `text-white`). Inline `style` only for computed values (e.g. budget bar width).

---

### FE-12 🟠 Lint, type-check and tests are gates

`npm run lint` (zero warnings), `tsc --noEmit`, `vitest --run` all pass in CI (`ci.yml` frontend job).
Tests use Testing Library queries by role/label, MSW for the API (including Pending → Graded), and a thin
wrapper around `useAuth` to toggle signed-in state.

---

### FE-13 🟠 Marketing site is static and test-free

Astro `output: 'static'`. The catalog is generated from `problems/*/problem.yaml` before `astro build` and
contains **summaries only** (title, track, difficulty, tags, statement excerpt) — never tests or reference
solutions. Deep links carry `?entry=` for attribution. **Same design, no client framework:** Tailwind v4 via
`@tailwindcss/vite` (never `@astrojs/tailwind`), the same `docs/design/tokens.css` imported by relative path
(never a copy), `.astro` components whose classes mirror the app's `cva` variants, colour roles only (FE-11),
icons from `lucide-static` inlined at build time, no React islands (shadcn/ui, zustand and lucide-react are
`apps/web` only — ADR 0007). Docker builds get `docs/design` and `problems/` as additional build contexts; the
final image contains `dist/` only.
**Check:** `check-rules.ps1` FE-11 + FE-13 (no React/shadcn/zustand imports, no `@astrojs/tailwind`) + RA-06.

---

### FE-14 🟡 No source maps or debug code in production builds

`build.sourcemap` off (or uploaded privately), no `console.log` left in committed code, verified by the
REA bundle audit before releases.

---

### FE-15 🔴 zustand holds client UI state only

Stores live in `src/stores/`, one per concern (`useThemeStore`, `useEditorLayoutStore`, `useDraftStore`).
They hold **UI state that the server doesn't own**: theme, pane sizes, per-problem drafts. Never API data
(that's TanStack Query — FE-03; copying it into a store creates a second, stale source of truth), never the
Clerk token or user identity (`useAuth()` owns them — FE-04), never anything secret. `persist` only with an
explicit `name` + `version`, and only for drafts/preferences. `devtools` middleware only behind
`config.isDev` (`export const isDev = import.meta.env.DEV` in `config.ts`, FE-05 — Vite inlines it as a
literal, so Rollup drops the branch and the devtools code from production). Components select
slices (`useStore((s) => s.x)`), not the whole store.
**Check:** `check-rules.ps1` FE-15 (no `getToken`/`token` in `stores/`; no unguarded `devtools(`) + RA-05
(no `__REDUX_DEVTOOLS_EXTENSION__` in `dist/`).

---

### FE-16 🟠 shadcn/ui is vendored, re-skinned source

`npx shadcn@latest add <component>` copies source into `src/components/ui/`; from then on it is **our code**:
reviewed in the PR, kept warning-free, upgraded deliberately (re-run `add` and diff), never edited for one
call site (wrap it in `components/` instead). Its CSS variables are mapped onto our colour roles by the shadcn
bridge in `docs/design/tokens.css`; shadcn's default palettes are never used (ADR 0006: our own design).
Generated variants are re-skinned to `docs/design/components.md` — e.g. no `text-white` on `bg-destructive`
(fails contrast in dark mode; use `bg-fail text-surface`). Keep Radix's keyboard and ARIA behaviour; don't
replace a primitive with a styled `div` (FE-10). Feature code imports our wrappers from `components/` where
one exists, otherwise `@/components/ui/*`.
**Check:** review; `check-rules.ps1` FE-11 (no raw palette classes in `components/ui`).

---

### FE-18 🟡 One brand: the mark and icons come from `docs/design/brand/`

The logo is `<Wordmark />` in each app (mark + `codebench`), never the name typed with a text caret. The mark's
geometry is defined once in `docs/design/brand/mark.svg`; the favicon and both `Wordmark` components use exactly
that geometry and the accent / on-accent roles. `favicon.svg` and `apple-touch-icon.png` in `apps/web/public` and
`apps/marketing/public` are byte-identical copies of `docs/design/brand/` — change the source, then copy.
**Check:** `check-rules.ps1` FE-18 (hash compare, geometry strings, no `▍` in app sources).

---

### FE-17 🟡 Icons from lucide-react, named imports only

`import { CircleCheck } from 'lucide-react'` — never `import * as Icons` or a string-keyed icon lookup
(`icons[name]`), which defeats tree-shaking and bloats the entry chunk. Decorative icons get
`aria-hidden="true"`; an icon that is the only content of a control needs an accessible name (`aria-label`
or visually hidden text). Icons never replace the status word (FE-10): ✓ + "Accepted", not ✓ alone.
**Check:** `check-rules.ps1` FE-17.

---

# Part 11 — Cross-Cutting Rules (XC)

### XC-01 🔴 Central Package Management — no versions in `.csproj`

Versions live only in `Directory.Packages.props`. **Check:** `check-rules.ps1` XC-01.

---

### XC-02 🟠 Analyzer-strict stays strict

`TreatWarningsAsErrors` + `AnalysisMode=All` + SonarAnalyzer for every project. A suppression
(`#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`) needs a comment saying why, on the same or
previous line. Generated code (migrations) is excluded via `.editorconfig`, not via pragmas.
**Check:** `check-rules.ps1` XC-02.

---

### XC-03 🟡 `async` all the way

No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`. **Check:** `check-rules.ps1` XC-03.

---

### XC-04 🟡 Nullable annotations are honest

`!` only where genuinely guaranteed. Prefer collection expressions (`[.. x]`), `ToUpperInvariant`,
`is null` / `is not null`.

---

### XC-05 🟠 Decisions are written down

A deviation from the guide or from this rulebook gets an ADR in `docs/adr/` (big) or a Decisions Log row in
the tracker (small), and — if it's a rule exception — a Known Deviations row below plus a baseline entry.

---

### XC-06 🟡 Generated code is never hand-edited

EF migrations, the model snapshot, and any generated API client. Change the source and regenerate.

---

# Part 12 — Naming Rules (NM)

One name per concept, spelled the same way in the file system, the code, the API, the database, the
queue and the UI. When a name is wrong it is cheaper to rename now than to explain forever.
Automated: `check-rules.ps1` checks NM-01, 02, 04, 05, 07, 08, 09, 10, 11, 12; the build enforces NM-03 (`.editorconfig` naming rules, IDE1006 as error); NM-06, NM-13 (until `apps/web` exists) and NM-14 are reviewed. All checks are **case-sensitive** (`-cmatch`).

### NM-01 🟠 One top-level type per C# file; file name = type name

`ProblemErrors.cs` contains `ProblemErrors` and nothing else. **Exception:** generic-arity twins of the
same name (`Result` + `Result<T>`, `ICommand` + `ICommand<T>`). `Program.cs`, `GlobalUsings.cs` and
migrations are exempt. Slices are covered too: `GetMe.cs` → `public static class GetMe` (VS-01).

**Why:** you find a type by its file name; a 4-type `Grading.cs` hides `Verdicts` from search and review.

---

### NM-02 🟠 Namespace = root namespace + folder path

| Project folder | Root namespace | Example |
|---|---|---|
| `src/Web.Api` | `Web.Api` | `src/Web.Api/Features/Users/GetMe.cs` → `Web.Api.Features.Users` |
| `src/Contracts` | `CodeBench.Contracts` | `src/Contracts/Verdicts.cs` → `CodeBench.Contracts` |
| `src/Worker` | `CodeBench.Worker` | `src/Worker/Sandbox/SandboxRunner.cs` → `CodeBench.Worker.Sandbox` |
| `src/Sandbox.Runner` | `CodeBench.Sandbox.Runner` | `…/Graders/Algorithms/…` → `CodeBench.Sandbox.Runner.Graders.Algorithms` |
| `tests/Web.Api.UnitTests` | `Web.Api.UnitTests` | `…/Users/GetMeHandlerTests.cs` → `Web.Api.UnitTests.Users` |
| `tests/IntegrationTests` | `IntegrationTests` | `…/Authentication/TestJwt.cs` → `IntegrationTests.Authentication` |
| `tests/ArchitectureTests` | `ArchitectureTests` | `…/Layers/BoundaryTests.cs` → `ArchitectureTests.Layers` |

Always file-scoped (`namespace X;`). The `Web.Api` vs `CodeBench.*` split is historical (Decisions Log
2026-11-17: template namespace kept) — don't "fix" one side without the other.

---

### NM-03 🟡 C# identifier casing

| Kind | Style | Examples |
|---|---|---|
| Types, methods, properties, events, enum members | `PascalCase` | `UserResolutionMiddleware`, `TryGetUserId`, `ProblemStatus.Published` |
| Interfaces | `I` + `PascalCase` | `IUserContext`, `IDateTimeProvider` |
| Constants and `static readonly` fields (any visibility) | `PascalCase` | `SectionName`, `CacheOptions`, `Placeholder` |
| Private instance fields | `_camelCase` | `_domainEvents`, `_dbContainer` |
| Parameters, locals | `camelCase` | `externalId`, `cancellationToken` |
| Generic type parameters | `T` or `T` + `PascalCase` | `T`, `TResponse`, `TCommand` |
| Booleans | `Is`/`Has`/`Can`/`Should` prefix, or `Try…` method returning `bool` | `IsDisabled`, `HasValidIssuer`, `TryGetUserId(out …)` |
| Enums | singular type name (plural only for `[Flags]`) | `Role`, `Difficulty`, `SubmissionKind` |

`CancellationToken` parameters are named `cancellationToken` (lambdas may use `ct`). Acronyms of three or
more letters are Pascal-cased words: `Json`, `Sql`, `Http`, `Nats`, `Url` (`ProblemJson`, not `ProblemJSON`);
two-letter ones too (`Id`, `Io`, `Ui`).
**Check:** `.editorconfig` naming rules at `warning` + `dotnet_diagnostic.IDE1006.severity = warning` → build error. Test methods are exempt from the PascalCase rule (`[tests/**/*.cs]`) because NM-11 governs them.

---

### NM-04 🟠 Async methods end in `Async`

Every method returning `Task`/`ValueTask` ends in `Async`; nothing else does.
**Exceptions (exhaustive):** the CQRS/domain-event contract method `Handle` (`ICommandHandler`,
`IQueryHandler`, `IDomainEventHandler`, decorators, dispatcher wrappers — template convention, decided
2026-11-19); xUnit test methods (NM-11); framework overrides whose name is fixed
(`BackgroundService.ExecuteAsync`, `IAsyncLifetime.InitializeAsync` already comply).

---

### NM-05 🟠 Middleware entry point is `InvokeAsync`

Convention-based middleware exposes `public Task InvokeAsync(HttpContext context, …)` — never `Invoke` —
and **awaits** `next(context)` when it holds scope (logging scopes, `using`) across the call.

**Why:** a non-awaited `return next(context)` inside `using` disposes the scope at the first `await`
downstream (found and fixed in `RequestContextLoggingMiddleware`, 2026-11-19).

---

### NM-06 🟡 Role suffixes say what a type is

| Suffix / name | Meaning | Lives in | Examples |
|---|---|---|---|
| `{UseCase}` (static class) | a vertical slice | `Features/{Feature}/` | `GetMe`, `CreateSubmission`, `PublishProblem` |
| `{Entity}` | aggregate / entity, singular | `Features/{Feature}/` | `User`, `Problem`, `TestCase` |
| `{Entity}Errors` | error catalog | `Features/{Feature}/` | `UserErrors`, `ProblemErrors` |
| `{Entity}{PastTenseVerb}DomainEvent` | domain event | `Features/{Feature}/` | `SubmissionGradedDomainEvent` |
| `{Feature}CacheKeys` | cache-key helpers | `Features/{Feature}/` | `UserCacheKeys`, `ProblemCacheKeys` |
| `Permissions` / `RolePermissionMatrix` | the one permission catalog / role grants | `Authorization/` | `Permissions.ProblemsPublish` |
| `{Entity}Configuration` | `IEntityTypeConfiguration<T>` | `Database/Configurations/` | `UserConfiguration` |
| `{Name}Seeder` | seeder | `Database/Seeding/` | `ProblemSeeder`, `RoleSeeder` |
| `{Name}Options` (+ `const string SectionName`) | bound configuration | next to its consumer | `ClerkOptions`, `SandboxOptions` |
| `{Name}Middleware` | ASP.NET middleware | `Common/Middleware/`, `Authentication/` | `UserResolutionMiddleware` |
| `{Name}Extensions` | static extension methods | next to what they extend | `AuthenticationExtensions` |
| `{Name}Publisher` / `{Name}Consumer` | NATS producer / consumer (hosted) | `Messaging/` (API), `src/Worker` | `OutboxPublisher`, `GradeResultConsumer` |
| `{Name}Provider` / `{Name}Validator` | injectable source of X / pure check | `Authorization/`, `Authentication/` | `PermissionProvider`, `AuthorizedPartyValidator` |
| `{Name}Decorator` | handler decorator | `Common/Behaviors/` | `ValidationDecorator` |
| `{Verb}{Noun}` contract record | NATS message | `src/Contracts/` | `GradeRequested`, `GradeCompleted` (past tense = fact) |

Slice names start with a verb: `Get` (read one/many), `Create`, `Update`, `Delete`, `Archive`,
`Publish`, `Validate`, `Import`/`Export`, `Handle{Source}Webhook`. A collection read is plural
(`GetProblems`), a single read singular (`GetProblem`). `DependencyInjection` (composition root) is the
one historical name outside this table.

---

### NM-07 🟡 Error codes, permissions, cache keys, policies

| Thing | Format | Examples |
|---|---|---|
| Error code | `"{FeaturePlural}.{Reason}"`, both `PascalCase` | `Users.NotProvisioned`, `Submissions.TooManyInFlight` |
| Permission | `"{area}:{action}"`, lowercase, kebab-case action | `problems:publish`, `submissions:read-own`, `admin:access` |
| Cache key | lowercase, `:`-separated, `{placeholder}` segments | `users:ext:{sub}`, `problems:list:{hash}`, `permissions:{externalId}` |
| Rate-limit policy | kebab-case | `submissions`, `anonymous-runs` |
| OpenAPI tag | `PascalCase` plural | `Users`, `Problems`, `Admin` |

**Check:** error codes and cache keys are pattern-checked (NM-07); permissions when `Permissions.cs` grows.

---

### NM-08 🟡 HTTP surface

Routes are lowercase, kebab-case, **plural nouns** for collections (`/problems`, `/me/submissions`,
`/admin/problems/{id}/tests/order`, `/webhooks/clerk`); no verbs except action sub-resources on a single
resource (`/admin/problems/{id}/publish`). Route parameters and query parameters are `camelCase`
(`{slug}`, `{id:guid}`, `?pageSize=`), JSON bodies `camelCase` (System.Text.Json web defaults), headers
`Title-Case` (`Idempotency-Key`, `Correlation-Id`).
**Check:** `Map{Get,Post,Put,Delete}("…")` literals must be lowercase kebab with `{camelCase}` params.

---

### NM-09 🟡 Data, messaging and config names

| Thing | Format | Examples |
|---|---|---|
| Tables / columns | snake_case, generated (don't hand-name — DM-03); tables plural | `users.external_id`, `outbox_messages.processed_at` |
| Migrations | `Phase{N}[A]_{Words_With_Underscores}` (`Initial` grandfathered) | `Phase1_Clerk_Users`, `Phase1_Problems_Users`, `Phase3A_Audit_Log` |
| NATS subjects | lowercase, `.`-separated, `{area}.{entity}.{event}` | `jobs.runs.created`, `results.graded` |
| NATS streams / durable consumers / buckets | `UPPERCASE` / kebab-case / kebab-case | `JOBS`, `api-results`, `problem-bundles` |
| Bundle keys | `{slug}/v{version}` | `sum-of-evens/v3` |
| Problem slugs | lowercase kebab-case, ≤ 64 chars | `merge-intervals` |
| Config keys | `PascalCase` sections (`Clerk:Issuer`), env vars `Section__Key`, `.env` vars `SCREAMING_SNAKE` | `Clerk__AuthorizedParties__0`, `CLERK_ISSUER` |
| Log templates | `PascalCase` properties, no interpolation | `"Outbox publish failed for {MessageId}"` |
| Telemetry / images / hosts | `codebench-{name}` / `codebench-{name}` / `{name}.codebench.localhost` | `codebench-api`, `codebench-sandbox-algorithms` |

**Check:** migration names (NM-09).

---

### NM-10 🟠 Test files mirror what they test

| Test kind | Path | Class |
|---|---|---|
| Handler unit test | `tests/Web.Api.UnitTests/{Feature}/{UseCase}HandlerTests.cs` | `{UseCase}HandlerTests` |
| Validator unit test | `tests/Web.Api.UnitTests/{Feature}/{UseCase}ValidatorTests.cs` | `{UseCase}ValidatorTests` |
| Other unit test | `tests/Web.Api.UnitTests/{SrcFolder}/{TypeName}Tests.cs` | `{TypeName}Tests` |
| Integration (slice) | `tests/IntegrationTests/{Feature}/{UseCase}Tests.cs` | `{UseCase}Tests` |
| Integration (cross-cutting) | `tests/IntegrationTests/{SrcFolder}/{Concern}Tests.cs` | e.g. `ClerkTokenValidationTests` |
| Architecture | `tests/ArchitectureTests/{Area}/{Topic}Tests.cs` | `BoundaryTests` |
| Test helpers | no `Tests` suffix | `TestJwt`, `BaseIntegrationTest` |

One test class per use case, not one per feature — the same unit as the slice (VS-01).
**Check:** every `*Tests.cs` declares a class of the same name (NM-01) and contains at least one test.

---

### NM-11 🟠 Test method names

`{Subject}_Should_{Outcome}[_When_{Condition}]`, each part `PascalCase`, no other underscores:
`Handle_Should_ReturnNotProvisioned_When_NoLocalUserWasResolved`, `GetMe_Should_Return401_When_Anonymous`,
`Worker_Should_NotReferenceDatabaseOrApi`. Subject = the method under test (unit), the slice name
(integration) or the component (architecture). Supersedes the shorter wording in TS-02.
**Check:** every `[Fact]`/`[Theory]` method (NM-11).

---

### NM-12 🟡 File and folder names outside C#

| Where | Style | Examples |
|---|---|---|
| `docs/**`, `scripts/**`, `deploy/**`, `sandbox-images/**`, `.github/**`, skills | kebab-case | `api-contract.md`, `check-rules.ps1`, `docker-compose.dev.yml` |
| ADRs | `NNNN-kebab-title.md` | `0006-no-katabench-recon.md` |
| Guide tabs | `NN-kebab-title.md` | `02-backend-api.md` |
| Sandbox images | `{graderKind}.Dockerfile` | `algorithms.Dockerfile` |
| Problems | `problems/{slug}/problem.yaml` | `problems/sum-of-evens/problem.yaml` |
| Hostile cases | `tests/hostile-submissions/{kebab-case}/Solution.cs` + `expected.json` | `infinite-loop/` |
| **Exception:** `context/` | snake_case (working docs referenced by name in prompts — decided 2026-11-19) | `build_plan.md`, `code_rules.md` |
| **Exception:** well-known names | as the ecosystem expects | `README.md`, `CLAUDE.md`, `SKILL.md`, `Dockerfile`, `Directory.Packages.props`, `.env.example` |

**Check:** NM-12 over `docs/`, `scripts/`, `deploy/`, `sandbox-images/`, `.github/`.

---

### NM-13 🟡 Frontend names (`apps/web`, `apps/marketing`)

| Thing | Style | Examples |
|---|---|---|
| React components | `PascalCase.tsx`, named export of the same name (FE-09) | `ProblemList.tsx` → `export function ProblemList` |
| Hooks | `useCamelCase.ts` | `useProblems.ts`, `useSubmitCode` |
| Other modules | `camelCase.ts` | `api.ts`, `queryClient.ts`, `config.ts` |
| Tests | `{File}.test.ts(x)` next to the file | `ProblemList.test.tsx` |
| Feature folders | lowercase, singular-or-plural as in `features/` list (FE-02) | `features/problems`, `features/editor` |
| zod schemas / inferred types | `camelCaseSchema` / `PascalCase` | `problemSummarySchema` → `type ProblemSummary` |
| Props types | `{Component}Props` | `ProblemListProps` |
| Query keys | arrays starting with the resource (FE-03) | `['problems', filters]` |
| Astro pages / content entries | kebab-case | `how-it-works.astro`, `blog/first-post.md` |
| CSS tokens | `--cb-{role}` vars → Tailwind `{utility}-{role}` | `--cb-ink-muted` → `text-ink-muted` |
| zustand stores | `stores/use{Concern}Store.ts`, hook of the same name | `useDraftStore.ts` → `export const useDraftStore` |
| shadcn/ui primitives | kebab-case as generated, in `components/ui/` only (KD-07) | `components/ui/dropdown-menu.tsx` |

**Check:** activates with `apps/web` (FE-* section of `check-rules.ps1`); `components/ui/` is excluded from
the PascalCase / one-component-per-file checks.

---

### NM-14 🔵 Commits and branches

Conventional Commits: `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`, `ci:` + optional scope
(`feat(problems): …`). Branches `{type}/{short-kebab}` (`feat/clerk-auth`). Not machine-checked locally;
add commitlint in Phase 3 if it drifts.

---

# Automated Verification

**→ Run `powershell -File scripts/check-rules.ps1`.** Exits non-zero on any violation, so it can gate CI.

**Status as of 2026-11-19: all checked rules pass, no baselined deviations** (KD-01 closed in Phase 1.1).

```
ok   AR-01  No Domain/Application/Infrastructure layer projects
ok   AR-02  Worker has no database access
ok   AR-03  Contracts references no package or project
ok   AR-04  Sandbox.Runner references only Contracts (+ Roslyn/EF for graders)
ok   AR-05  No cross-feature Command/Query/Handler/Validator/Endpoint references
ok   AR-06  Web.Api does not reference Worker or Sandbox.Runner
ok   AR-08  No string-literal NATS subjects outside Contracts
ok   VS-01  Slice file = public static class {UseCase}
ok   VS-02  Handlers are internal sealed
ok   VS-03  No manual slice wiring, MediatR or IApplicationDbContext
ok   VS-05  Every Command has a nested Validator
info VS     slices=5 commands=3 queries=2
base DM-04  No DateTime.Now/UtcNow outside DateTimeProvider (1 accepted)
ok   DM-05  No [Table]/[Column]/[Key]/[MaxLength]/[Required] in Features
ok   EP-01  No throw in slice handlers (return Result.Failure)
ok   EP-03  Endpoints contain no data access
base EP-04  Every endpoint makes an explicit auth decision (3 accepted)
ok   EP-05  Every endpoint tagged via Tags constants
skip SB-01  Sandbox containers fully hardened (no SandboxRunner yet)
ok   SB-02  Host Docker socket never used by Worker
ok   SC-01  No secret values in source
skip FE-*   Frontend rules (apps/web not scaffolded yet)
ok   NM-01  One top-level type per file, named like the file
ok   NM-02  Namespace = root namespace + folder path (file-scoped)
ok   NM-04  Async suffix exactly on Task-returning methods (Handle excepted)
ok   NM-05  Middleware exposes InvokeAsync, not Invoke
ok   NM-07  Error codes Feature.Reason; cache keys lowercase a:b:{c}
ok   NM-08  Routes are lowercase kebab-case with {camelCase} parameters
ok   NM-09  Migrations named Phase{N}_{Words}
ok   NM-10  Test files mirror what they test
ok   NM-11  Test methods named {Subject}_Should_{Outcome}[_When_{Condition}]
ok   NM-12  kebab-case names in docs/, scripts/, deploy/, problems/, skills, CI
ok   XC-01  Central Package Management: no Version= in csproj
ok   XC-02  Every #pragma warning disable has a justification comment
ok   XC-03  No sync-over-async
```

### The baseline mechanism

`$Baseline` in the script maps a rule id to accepted `File.cs[:Line]` locations. A baselined hit reports
`base`; a hit anywhere else still fails, so a **new** instance of the same anti-pattern is caught. Every
baseline entry must have a KD-xx row below, and is deleted when its exit condition is met.

### Compiled checks (run with the test suite)

| Rule | Test |
|---|---|
| AR-02, AR-03 | `tests/ArchitectureTests/Layers/BoundaryTests.cs` + `scripts/rea-audit.ps1` RA-01/RA-02 (built DLL + `deps.json`) |
| AR-04 | `scripts/rea-audit.ps1` RA-03 |
| VS-02 (handlers/endpoints sealed) | `tests/ArchitectureTests/Layers/LayerTests.cs` |
| QY-04, EP-04 (admin), EP-07, DM-07, TS-03 | integration tests (added per phase) |
| SB-01..SB-06 | `tests/hostile-submissions/` via `scripts/test-sandbox.sh` (Phase 2) |

### Standard gates

```powershell
dotnet build CodeBench.slnx -c Release                    # zero warnings
dotnet test CodeBench.slnx                                # all green (Docker running)
powershell -File scripts/check-rules.ps1                  # rulebook (source)
powershell -File scripts/rea-audit.ps1                    # rulebook (shipped artefacts, REA) — RA-01..09, build plan Appendix D
dotnet list package --vulnerable --include-transitive     # audit debt
npm --prefix apps/web run lint; npm --prefix apps/web test -- --run   # once the app exists
```

---

# Review Checklist (paste into a PR)

```markdown
## Boundaries
- [ ] AR-02/03/04  Worker no DB · Contracts BCL-only · Runner refs Contracts only
- [ ] AR-05        No cross-feature slice references
- [ ] AR-07/08     Infra outside Features/ · no magic strings (subjects, tags, permissions, cache keys)

## Slices
- [ ] VS-01/02  One file, fixed nested shapes
- [ ] VS-04     Handler = use-case logic only
- [ ] VS-05     Every new command has a Validator
- [ ] VS-06/07  CancellationToken everywhere · no identity in request bodies

## Domain & data
- [ ] DM-01/02  Events raised in entity · guarded, idempotent transitions
- [ ] DM-03/05  Config in Database/Configurations · no attributes on entities
- [ ] DM-04     IDateTimeProvider, not DateTime.UtcNow
- [ ] DM-06     Migration created via tooling, applied ones untouched

## Queries & caching
- [ ] QY-01/02  Projection to Response · no client-side evaluation
- [ ] QY-04     No hidden tests / reference solution on public routes
- [ ] QY-05     Cache keys from {Feature}CacheKeys · invalidation in every mutating command

## Endpoints & errors
- [ ] EP-01/02  Result + {Entity}Errors with correct ErrorType
- [ ] EP-03     Endpoint thin
- [ ] EP-04     Explicit auth decision
- [ ] EP-06/07  202 + idempotency + rate limit on work-creating writes · ownership enforced

## Pipeline & sandbox
- [ ] MQ-01..04 Outbox only · MsgId · idempotent consumers · ack last
- [ ] SB-01..06 Hardened containers · no host socket · timeout + finally remove · nonce parsing · hostile suite

## Security
- [ ] SC-01..06 No secrets · Clerk azp · roles in DB · explicit CORS · dev data dev-only

## Frontend
- [ ] FE-01/03/04 Strict TS · TanStack Query · all HTTP via lib/api.ts
- [ ] FE-15/16/17 zustand = UI state only · shadcn re-skinned via tokens · lucide named imports
- [ ] FE-05/08    Public config only · client guards are UX only
- [ ] FE-10/12    Accessible · lint/type/tests green

## Tests
- [ ] TS-01/02  Right test type, existing naming
- [ ] TS-03     Security-relevant behaviour has an integration test

## Naming
- [ ] NM-01/02  One type per file, file = type · namespace = folder path
- [ ] NM-04/05  Async suffix (Handle excepted) · middleware InvokeAsync
- [ ] NM-06/07  Role suffixes · error codes, permissions, cache keys
- [ ] NM-08/09  Routes kebab + {camelCase} · migrations Phase{N}_{Words}
- [ ] NM-10/11  Test files mirror slices · {Subject}_Should_{Outcome}[_When_{Condition}]

## Cross-cutting
- [ ] XC-01/02  No versions in csproj · suppressions justified
- [ ] XC-05     Deviations recorded (ADR / Decisions Log / KD)
- [ ] scripts/check-rules.ps1 passes

**Score:** ___ / 100
```

---

# Known Deviations (living list)

| ID | Deviation | Rationale | Exit condition |
|---|---|---|---|
| ~~KD-01~~ | ~~Template JWT issuing still present~~ | — | ✅ **Closed 2026-11-19** (Phase 1.1): slices, `TokenProvider`, `RefreshToken`, password hashing, `Jwt` config and `JWT_SECRET` deleted; baseline entries removed |
| KD-02 | `IDateTimeProvider.UtcNow` returns `DateTime` (UTC), guide uses `TimeProvider`/`DateTimeOffset` | Template convention; Npgsql maps UTC `DateTime` to `timestamptz` | None — deliberate. Revisit only if offsets are ever needed |
| KD-03 | Decorators registered with `TryDecorate` | Template's `Decorate` throws when a handler shape has no implementations (happened after removing Todos) | None — deliberate |
| KD-04 | Observability is one `grafana/otel-lgtm` container, not the five services in guide 05 | Same signals, one container, already in the template | Phase 4.2 if separate retention/scaling is needed |
| KD-05 | EF migrations marked `generated_code` in `.editorconfig` | Tool output fails IDE0161 under analyzer-strict | None — deliberate |
| KD-06 | Hidden tests travel over NATS (object store), not baked into images | Admin edits without image rebuilds (guide 09, ADR 0005) | None — deliberate; `backend` network stays internal-only |
| KD-07 | `src/components/ui/` (shadcn/ui) uses kebab-case files with several exports each (`card.tsx` → `Card`, `CardHeader`, …) and exports `cva` variants | Upstream shadcn layout; renaming breaks `npx shadcn add`/diff-based upgrades. Exempt from FE-09 / NM-13 file rules and from `react-refresh/only-export-components` (ESLint override for that folder only) | None — deliberate; everything outside `components/ui/` follows FE-09 / NM-13 |

---

# Changelog

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-11-18 | Initial rulebook for CodeBench: VSA template conventions + guide 00–09 boundaries + frontend standards; `scripts/check-rules.ps1` |
| 1.1 | 2026-11-19 | Part 12 Naming (NM-01…NM-14) + 10 automated checks; `.editorconfig` naming enforced (IDE1006); XC-02 check fixed (was vacuous: `#` lines skipped, `tests/` not scanned); TS-02 points to NM-11; KD-01 closed |
| 1.2 | 2026-11-22 | Frontend stack adds shadcn/ui, lucide-react, zustand: FE-15 (zustand = client UI state only), FE-16 (shadcn vendored + re-skinned via the token bridge), FE-17 (lucide named imports); FE-02 tree, FE-09/FE-11/NM-13 updated; KD-07 (`components/ui/` naming exception); RA-05 also rejects devtools hooks; check-rules FE-11/15/17 |
| 1.4 | 2026-11-25 | FE-18 one brand: mark + icons from `docs/design/brand/` (favicon, apple-touch-icon, `Wordmark` geometry); check-rules FE-18 (probe-tested) |
| 1.3 | 2026-11-23 | FE-13 extended: Astro uses Tailwind v4 via `@tailwindcss/vite`, the same `tokens.css` by relative import, `.astro` components mirroring the app's variants, `lucide-static`, no client framework; additional Docker build contexts; check-rules FE-11 + FE-13 cover `apps/marketing` |

> **Keeping this current:** when you accept a new trade-off, add it to *Known Deviations* with an exit
> condition. When you add a rule, give it an ID, a severity, a *why*, and a check. A rule without an
> automated check will not be followed.
