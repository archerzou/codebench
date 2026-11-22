# CodeBench — notes for Claude Code

CodeBench is a C# practice platform (learning project): problem catalog, Monaco editor, Run/Submit, and an
asynchronous grading pipeline (API → outbox → NATS JetStream → Worker → throwaway sandbox container → results).
Built on the Vertical Slice Architecture template. The build guide is in `docs/guide/` (read
`00-overview.md` first, work from `07-roadmap.md`); decisions are in `docs/adr/`, the endpoint design in
`docs/api-contract.md`. Where guide tabs disagree, **`09-seed-data-and-admin.md` wins** (see ADR 0005).

Working documents in `context/`: **`build_plan.md`** (what to build next, step by step), **`code_rules.md`**
(the full rulebook with rule IDs — review changes against it), **`progress_tracker.md`** (tick items as they
are done; update the summary counts and Decisions Log). Run `powershell -File scripts/check-rules.ps1` before
calling work done.

## Repository layout (monorepo)

```text
apps/marketing        Astro static site (codebench.localhost)
apps/web              React + Vite + TS app incl. /admin (app.codebench.localhost) — Tailwind v4, shadcn/ui, lucide-react, zod, TanStack Query, zustand (ADR 0007)
src/Web.Api           .NET 10 API — vertical slices (namespace Web.Api)
src/Contracts         NATS message records shared by API + Worker (CodeBench.Contracts) — BCL only
src/Worker            grading orchestrator (CodeBench.Worker) — NATS + sandbox daemon only, NO database
src/Sandbox.Runner    grader harness inside sandbox images (CodeBench.Sandbox.Runner)
sandbox-images/       one Dockerfile per grader kind
problems/<slug>/problem.yaml        seed data only (loaded once by the seeder); the DB is the source of truth afterwards
tests/                Web.Api.UnitTests, IntegrationTests (Testcontainers), ArchitectureTests, hostile-submissions/
deploy/               docker-compose.yml (+ .dev.yml), .env.example; real deploy/.env is git-ignored
docs/                 guide/, adr/, recon.md, feature-map.md, api-contract.md
```

### Inside `src/Web.Api`

There is **no** Domain/Application/Infrastructure/SharedKernel split.

- **`Features/{Entity}/`** — one folder per feature (`Problems`, `Submissions`, `Users`, `Progress`, `Admin`). Each **use case is one file**: `Features/{Entity}/{UseCase}.cs` is a `public static class {UseCase}` nesting the whole slice — `Command`/`Query`, `Validator` (commands only), an `internal sealed Handler`, and an `Endpoint : IEndpoint`. The entity, its `{Entity}Errors` catalog, and domain events live alongside the slices in the same feature folder.
- **`Common/`** — cross-cutting building blocks: `Result`/`Result<T>`, `Error`/`ErrorType`/`ValidationError`, `Entity`, `IDomainEvent`, `IDateTimeProvider` (`Web.Api.Common`); `ICommand`/`IQuery` + handler interfaces (`Common.Messaging`); validation + logging decorators (`Common.Behaviors`); `IEndpoint` + `Tags` + endpoint extensions (`Common.Endpoints`); `PagedList<T>`, `IdempotencyFilter`, security headers; and migration/observability/rate-limiting/result extensions (`Common.Extensions`).
- **`Database/`** — `ApplicationDbContext`, EF configurations in `Database/Configurations/`, migrations in `Database/Migrations/`, the domain-event dispatcher, seeders in `Database/Seeding/`.
- **`Messaging/`** — NATS JetStream infrastructure: outbox publisher, graded-result consumer, stream/object-store setup, problem-bundle publisher. (Distinct from `Common/Messaging`, which is the in-process CQRS interfaces.)
- **`Authentication/`** / **`Authorization/`** — Clerk session-token validation (issuer/JWKS, lifetime, `azp`; ADR 0001), `IUserContext` (`ExternalId` = Clerk `sub`, `UserId` resolved per request by `UserResolutionMiddleware`, cached `users:ext:{sub}`), permission-based authorization that fails closed (permissions from our DB). The API never issues tokens.
- **`Notifications/`** — `IEmailSender` + `LoggingEmailSender`.
- **`BackgroundJobs/`** — hosted `BackgroundService` workers (add as needed; see `/add-background-job`).

## Conventions

- **One file per use case.** `Features/{Entity}/{UseCase}.cs`, namespace `Web.Api.Features.{Entity}`, a `public static class {UseCase}` with nested `Command`/`Query`, `Validator`, `internal sealed Handler`, and `Endpoint`. No separate `Endpoints/` folder.
- **CQRS, no MediatR** — `ICommand`/`ICommand<T>` + `ICommandHandler<...>`, `IQuery<T>` + `IQueryHandler<Q,R>` (`Web.Api.Common.Messaging`). Handlers are `internal sealed` with primary constructors, auto-registered by Scrutor scanning; wrapped by `ValidationDecorator` + `LoggingDecorator` (via `TryDecorate`). Validators come from `AddValidatorsFromAssembly(includeInternalTypes: true)`, endpoints from `AddEndpoints`. Never wire a slice up by hand.
- **No cross-feature coupling.** A slice may use the shared `Common`/`Database`/`Messaging`/`Authentication`/`Authorization` code and another feature's **domain types** (entity, `{Entity}Errors`, domain events), but never another feature's `Command`/`Query`/`Handler`/`Validator`/`Endpoint`.
- **Errors, not exceptions** — return `Result.Failure(...)` with an `Error` for expected failures. Error codes are `"{FeaturePlural}.{Reason}"` from static `{Entity}Errors` factories. Endpoints translate with `result.Match(Results.Ok, CustomResults.Problem)` (work-creating writes return `Results.Accepted`).
- **Endpoints** implement `IEndpoint` (nested in the slice), resolve the handler interface from DI, tag with `Tags`, and protect with `.RequireAuthorization()` / `.HasPermission(...)`; opt into dedupe with `.WithIdempotency()` (honors an `Idempotency-Key` header). Admin slices map under the `/admin` group requiring `admin:access`.
- **Validation** — FluentValidation `AbstractValidator<Command>` nested as `Validator`; runs in the validation decorator (commands only — queries have no validator).
- **Data — inject the concrete `ApplicationDbContext`** (from `Web.Api.Database`) directly into handlers. **There is no `IApplicationDbContext`.** Entities use FK ids (shadow relationships), no navigations; configurations in `Database/Configurations/` are auto-applied. snake_case naming is on.
- **List queries** return `PagedList<T>` (search + sort + page); queries project to a nested `Response` DTO with `.Select(...)`, never returning entities.
- **Caching** — HybridCache (Redis as L2) with per-feature `{Feature}CacheKeys` helpers + invalidation in the commands that mutate the cached data.
- **Observability** — Serilog + OpenTelemetry over OTLP to the `observability` container (Grafana/Loki/Tempo/Prometheus). Grafana at http://localhost:3000 with the dev override, or http://grafana.codebench.localhost.

## CodeBench invariants (security boundaries — never break these)

- **The Worker has no database access**: no EF Core/Npgsql reference, no connection string, no Clerk/payment secrets. Enforced by `ArchitectureTests/Layers/BoundaryTests`.
- **Message contracts live only in `src/Contracts`** and depend on the BCL only. Subjects: `jobs.runs.created`, `jobs.submissions.created`, `results.graded`.
- **Hidden tests and reference solutions never reach the browser.** Public endpoints return visible tests only; hidden results expose name, pass/fail, timings and a generic message. They travel API → NATS object store (`problem-bundles`, key `{slug}/v{version}`) → Worker → sandbox stdin.
- **Submissions are enqueued through the outbox** (submission + outbox row in one `SaveChangesAsync`), never by publishing to NATS inside a request.
- **User code runs only in a sandbox container** on the `sandbox-daemon` (never the host socket) with no network, read-only rootfs, dropped caps, non-root, pids/memory/CPU limits and a worker-enforced timeout.
- **Roles/permissions live in our DB** (`learner`/`author`/`admin`); Clerk claims are UI hints only.
- **Secrets**: only `*.env.example` is committed. Nothing named `*SECRET*`, server `*API_KEY*` or `*PASSWORD*` goes into the web app's runtime `config.js`.

## Build, run & test

- `dotnet build CodeBench.slnx`
- `dotnet test CodeBench.slnx` — integration tests use Testcontainers, so Docker must be running.
- Infra for local dev: `docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d`, then `dotnet watch --project src/Web.Api`. Compose profiles: `app`, `grading`, `backup`.
- **Analyzer-strict**: `TreatWarningsAsErrors` + `AnalysisMode=All` + SonarAnalyzer, for every project. Keep code warning-clean; prefer collection expressions (`[.. x]`) and `ToUpperInvariant`.

## Skills (prefer these for common tasks)

`/add-feature` (use case) · `/add-entity` · `/add-tests` · `/add-migration` · `/add-background-job` (outbox publisher, results consumer, janitor) · `/vsa-review` (convention review).
CodeBench-specific: `/add-problem` · `/add-grader` · `/add-react-feature` · `/add-admin-crud` · `/add-astro-page` · `/add-compose-service` · `/run-hostile-suite`.
Plugins (project scope, `.claude/settings.json`): csharp-lsp, typescript-lsp, security-guidance, commit-commands, pr-review-toolkit, github. `/bootstrap` has already been run (renamed to CodeBench, Todos removed).
