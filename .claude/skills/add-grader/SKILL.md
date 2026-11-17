---
name: add-grader
description: Add a new grader kind to CodeBench's grading pipeline — runner code in src/Sandbox.Runner/Graders, its sandbox image in sandbox-images/, worker/compose/CI wiring, problem.yaml fields, UI for its Details, and hostile-suite cases. Use when the user wants a new kind of grading (refactoring metrics, architecture rules, secure coding, test writing, database plans).
argument-hint: <grader kind and what it measures, e.g. "refactoring: complexity + nesting thresholds">
---

# Add a Grader

Read guide 03 ("The grader harness", "Database grader", "Other graders") and the SB-* rules in
`context/code_rules.md` first. A grader is **harness code + an image + wiring**; the worker and the
API stay generic.

## Workflow

1. **Name the kind** (lowercase, e.g. `refactoring`). It appears in `problem.yaml` `grader:`,
   `Problem.GraderKind`, `GradeRequested.GraderKind`, `Sandbox:Images:<kind>` and the image name
   `codebench-sandbox-<kind>`.
2. **Runner code** in `src/Sandbox.Runner/Graders/<Kind>/`:
   - Reuse the shared harness (stdin bundle + code, Roslyn compile, collectible ALC, nonce output). Never
     add a second output path.
   - Implement the grader's checks; put structured evidence (SQL, plan summary, metrics, rule outcomes)
     in `TestOutcome.Details` as JSON.
   - Selected by `--grader <kind>`; unknown kind → `InternalError`.
   - New packages go in `Directory.Packages.props` only; the runner must still not reference `Web.Api` or
     `Worker` *[AR-04]*.
   - Unit-test every rule/metric with fixed inputs.
3. **Bundle fields** — if the grader needs new problem data (e.g. `metrics`, `planRules`), add it to the
   `Problem` entity/configuration, the seeder's `ProblemDefinition`, the bundle serializer, the admin editor
   "Rules" tab, and document it in the `add-problem` skill. Contracts change only additively *[MQ-05]*.
4. **Image** `sandbox-images/<kind>.Dockerfile`: same pattern as `algorithms.Dockerfile` — non-root uid
   10001, `WORKDIR /tmp`, entrypoint `--grader <kind>`, nothing writable outside `/tmp`, no problems baked in.
   If it needs a bigger tmpfs (database), make that a per-kind worker option, not a global change.
5. **Wiring**
   - `deploy/docker-compose.yml` worker env: `Sandbox__Images__<kind>`; `src/Worker/appsettings.json` key.
   - `.github/workflows/sandbox-images.yml` matrix `kind`.
   - Hardening flags unchanged *[SB-01]* — a grader never gets network, extra caps or the host socket.
6. **Hostile suite** — add cases under `tests/hostile-submissions/` that target this grader (e.g. for
   database: `DROP TABLE`, `pg_sleep`, reading `/tmp/pgdata`; for refactoring: metric-gaming tricks), then
   run the `run-hostile-suite` skill.
7. **UI** — render the grader's `Details` in the results panel (`apps/web/src/features/submissions/`), and
   any extra budgets on the statement pane.
8. **Problem** — add at least one original problem for it with the `add-problem` skill.
9. **Tracker** — tick the matching Phase 5/6 items in `context/progress_tracker.md`.

## Done when

Reference solution → Accepted; a known-bad solution fails for the intended reason with readable
`Details`; hostile suite passes for the new image; `dotnet test` and `scripts/check-rules.ps1` pass.
