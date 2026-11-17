---
name: add-problem
description: Author a new CodeBench practice problem as problems/<slug>/problem.yaml — original statement, signature, starter code, reference solution, visible + hidden data-driven tests, generators and budgets — then seed and validate it. Use when the user wants to add, write, or draft a problem, challenge, kata, or exercise.
argument-hint: <problem idea, e.g. "longest run of equal chars, algorithms, easy">
---

# Add a Problem

Problems are YAML **seed data** in `problems/<slug>/problem.yaml` (guide 09 "Data-driven test format",
"The six seed problems"; ADR 0005). The seeder inserts each slug **once**; afterwards the database (and the
admin console) owns the content. Use the six existing files as the reference shape.

## Rules

- **Original work only.** Write the statement, tests and reference yourself. Classic algorithm *ideas*
  are fine; never copy statements, tests or solutions from Katabench or any other site (guide 00).
- **Hidden tests must add something**: edge cases and the performance input — not copies of visible ones.
- **Budgets come from measurement**, not guesses: set them at ~2–3× the reference's median (Validate).
- **The grader must exist.** `grader:` is `algorithms` (Phase 2), `database` (Phase 5) or `refactoring`
  (Phase 6). For a grader that isn't built yet, write the file but note it can't be seeded/validated yet.

## Workflow

1. **Pick the slug** (kebab-case, unique — check `problems/`), `track`, `difficulty`, `grader`, `free`, `tags`.
2. **Signature first.** `public class Solution` with one public method; `signature:` is that method's
   declaration exactly. Parameter and return types must round-trip through `System.Text.Json`.
3. **Statement** — Markdown, precise constraints (input ranges, ordering, tie-breaks), and state any
   allocation or plan budget the learner must meet.
4. **Starter** — compiles, same signature, body `throw new NotImplementedException();` (refactoring
   problems: the starter *is* the behaviour reference).
5. **Reference** — correct, idiomatic, within budget. Database problems: also `schema`, `seedSql` (enough
   rows for a realistic plan, end with `ANALYZE`), `model`, `planRules`. Refactoring: `metrics`.
6. **Tests** — 3+ visible (incl. the example from the statement), 2+ hidden. Fields:
   `name`, `hidden`, `args` (JSON array, one entry per parameter), `expected` or `{ $ref: reference }`
   or `expectedException`, `compare` (`exact` | `unordered` | `unorderedNested` | `approx`),
   `timeBudgetMs`, `allocBudgetBytes`. Large inputs use a generator with a fixed `seed`:
   `{ $gen: randomInts|randomStrings|randomIntervals|sortedInts, ..., seed: N }`.
7. **Limits** — `limits: { timeoutSeconds: 10, memoryMb: 256 }` (database: 20 / 512).
8. **Check the YAML parses** and matches the existing files' keys:
   ```powershell
   npx --yes js-yaml problems/<slug>/problem.yaml > $null
   ```
9. **Seed / validate** (once Phase 1 seeding exists):
   - New slug: `docker compose -f deploy/docker-compose.yml --env-file deploy/.env run --rm api --seed`.
   - Slug already in the DB: seeding skips it — use the admin console's **Import YAML**, then **Validate**.
   - Once Phase 3A exists, run **Validate** and set budgets from the measured reference medians.
10. If it's a free problem shown on the marketing site, rebuild `apps/marketing` (catalog is generated).

## Done when

The YAML parses; reference passes all tests within budget (Validate); a naive solution fails the hidden
performance/edge tests for the intended reason; no hidden data appears in `GET /problems/{slug}`.
