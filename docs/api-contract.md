# API contract (CodeBench)

Our own design, consolidated from [guide/02-backend-api.md](guide/02-backend-api.md) and
[guide/09-seed-data-and-admin.md](guide/09-seed-data-and-admin.md). Errors are RFC 7807 ProblemDetails
(`CustomResults.Problem`); error codes are `"{FeaturePlural}.{Reason}"`. Writes that create work honour
an `Idempotency-Key` header. No third-party API informed this design (ADR 0006); reviewed 2026-11-18
against build plan §1.6/§2.2, guides 02/09 and `src/Contracts` (subjects and verdicts match).

## Public / learner

| Method | Route | Body / query | Response | Auth | Slice | Phase |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/problems` | `track`, `difficulty` (names, any case; unknown → 400 `Problems.InvalidFilter`), `q` (title/slug/tag), `sort` (`recommended` · `title` · `difficulty` · `newest`; unknown → `recommended`), `page` (≥ 1), `pageSize` (1–100, default 20) | `PagedList<ProblemSummary>` — Published only, cached 10 min | anonymous | `Problems/GetProblems` | 1 |
| GET | `/problems/{slug}` | – | `ProblemDetail` (statement, starter, signature, limits, **visible** tests, hidden count) · 404 `Problems.NotFound` if missing, not Published, or Pro and the caller isn't Pro/staff | anonymous; non-free problems: `plan = pro` or `admin:access` | `Problems/GetProblem` | 1 |
| GET | `/me` | – | `{ id, email, role, plan, permissions[] }` | signed in | `Users/GetMe` | 1 |
| POST | `/webhooks/clerk` | Svix-signed Clerk event (`user.created` / `user.updated` / `user.deleted`; others ignored) | 204 applied or ignored · 400 bad signature / payload · 503 `Clerk:WebhookSecret` unset | signature | `Users/HandleClerkWebhook` | 1 |
| POST | `/runs` | `{ problemId, code }` | 202 `{ id }`, `Location: /submissions/{id}` | anonymous for free problems (`anonymous-runs` limit); non-free problems need sign-in (anonymous → 401) | `Submissions/CreateRun` | 2 |
| POST | `/submissions` | `{ problemId, code }` | 202 `{ id }` | signed in (`submissions` limit, ≤ 2 in flight) | `Submissions/CreateSubmission` | 2 |
| GET | `/submissions/{id}` | – | `SubmissionResult` | owner only (foreign id → 404); anonymous runs readable by their unguessable id | `Submissions/GetSubmission` | 2 |
| GET | `/me/submissions` | `problemId, page` | `PagedList<SubmissionSummary>` | signed in | `Submissions/GetMySubmissions` | 2 |
| GET | `/me/progress` | – | `Progress` | signed in | `Progress/GetProgress` | 7 |
| GET | `/health` | – | health report (Postgres, Redis, NATS) | anonymous | – | 1 |

### Shapes

```text
PagedList<T>     { items[], page, pageSize, totalCount, hasNextPage, hasPreviousPage }
ProblemSummary   { id, slug, title, track, difficulty, isFree, tags[], solved? (Phase 7) }
ProblemDetail    { id, slug, title, track, difficulty, isFree, version, signature, statementMarkdown,
                   starterCode, limits { timeoutSeconds, memoryMb },
                   visibleTests[] { name, args (JSON), expected? (JSON), expectedException?, compare,
                                    timeBudgetMs?, allocBudgetBytes? },
                   hiddenTestCount, planRules[]?, metrics[]? }
SubmissionResult { id, problemId, problemVersion, kind (Run|Submit),
                   status (Pending|Running|Graded|Failed),
                   verdict? (Accepted|WrongAnswer|CompileError|Timeout|BudgetExceeded|InternalError),
                   compilerOutput?, createdAt, gradedAt?,
                   tests[] { name, hidden, passed, elapsedMicros, allocatedBytes, message?, details? } }
Me               { id, email, role (learner|author|admin), plan (free|pro), permissions[] }
```

Enums are camelCase strings everywhere: `track` (algorithms · database · refactoring · architecture ·
secureCoding · testWriting), `difficulty` (easy · medium · hard), `compare` (exact · unordered ·
unorderedNested · approx). Errors are `application/problem+json`; an unbindable query value is a 400.

Hidden tests: only `name`, `passed`, timings and a generic message — never args or expected values.
`ProblemDetail` never contains hidden tests or the reference solution (integration-tested).

## Admin (`/admin`, requires `admin:access`)

| Method | Route | Extra permission | Slice |
| --- | --- | --- | --- |
| GET | `/admin/stats?days=14` | `stats:read` | `GetAdminStats` |
| GET | `/admin/problems?status=&track=&q=&page=` | – | `ListProblems` |
| GET | `/admin/problems/{id}` (incl. hidden tests, reference) | – | `GetProblemForEdit` |
| POST | `/admin/problems` (creates Draft) | `problems:write` | `CreateProblem` |
| PUT | `/admin/problems/{id}` with `If-Match` | `problems:write` | `UpdateProblem` |
| POST | `/admin/problems/{id}/validate` | `problems:write` | `ValidateProblem` |
| POST | `/admin/problems/{id}/publish` | `problems:publish` | `PublishProblem` |
| POST | `/admin/problems/{id}/archive` | `problems:publish` | `ArchiveProblem` |
| DELETE | `/admin/problems/{id}` (Drafts only) | `problems:delete` | `DeleteProblem` |
| POST/PUT/DELETE | `/admin/problems/{id}/tests[/{testId}]` | `tests:write` | `CreateTestCase`, `UpdateTestCase`, `DeleteTestCase` |
| PUT | `/admin/problems/{id}/tests/order` | `tests:write` | `ReorderTestCases` |
| POST / GET | `/admin/problems/import`, `/admin/problems/{id}/export` | `problems:write` | `ImportProblemYaml`, `ExportProblemYaml` |
| GET / PUT | `/admin/users…` | `users:manage` | `ListUsers`, `ChangeUserRole`, `DisableUser` |
| GET / POST | `/admin/submissions…` | `submissions:read-all` | `ListAllSubmissions`, `GetAnySubmission`, `RegradeSubmission` |
| GET | `/admin/audit?entity=&actor=` | `stats:read` | `ListAuditLog` |

Conflicts: stale `If-Match` → 409; publish without a passing validation → 409.

## Messaging (NATS JetStream)

| Stream | Subjects | Durable consumer | Consumer |
| --- | --- | --- | --- |
| `JOBS` | `jobs.runs.created`, `jobs.submissions.created` | `graders` | Worker(s) |
| `RESULTS` | `results.graded` | `api-results` | API |

Object store `problem-bundles`: key `{slug}/v{version}`. Records: `src/Contracts` (`GradeRequested`, `GradeCompleted`, `TestOutcome`).
