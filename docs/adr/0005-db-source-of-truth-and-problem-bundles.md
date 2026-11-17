# 5. Database is the source of truth; graders receive versioned problem bundles

Status: accepted · 2026-11-17 (supersedes the "tests baked into sandbox images from `problems/`" design in guide tabs 00/02/03)

## Context
Admins edit problems and tests at runtime. Baking hidden tests into sandbox images would require an
image rebuild per edit, and the worker must never touch the database.

## Decision
- `problems/*/problem.yaml` is only the **initial seed**; the seeder inserts and never overwrites.
- Tests are mostly data (`args`, `expected`, `compare`, budgets, optional `$gen` generators).
- On publish, the API writes `{slug}/v{version}` (signature, all tests, schema, seedSql, model, rules,
  metrics, limits) to the NATS object store `problem-bundles`. `GradeRequested` carries `ProblemVersion`.
- The worker fetches + caches bundles with its NATS credentials and pipes bundle + code into the sandbox.

## Consequences
- In-flight submissions grade against the version they were submitted for.
- Hidden tests still never reach the browser, but do travel over NATS (internal `backend` network).
- Sandbox images contain only the harness, so they rebuild only when `src/Sandbox.Runner` changes.
