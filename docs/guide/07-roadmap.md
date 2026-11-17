# Roadmap and build checklist

Build in eight phases, each ending in something that runs: a walking skeleton first, then the algorithms grading pipeline end to end, then CI/CD, and only then the harder graders and product features. Tick items off as you go.

| Phase | Goal | Done when |
| --- | --- | --- |
| 0 | Recon and setup | `docs/recon.md`, `feature-map.md`, `api-contract.md` committed; monorepo builds |
| 1 | Walking skeleton | Signed in through Traefik URLs, you open a problem and see its starter code |
| 2 | Algorithms grading | Two Sum submissions return per-test time and allocations; hostile suite passes |
| 3 | CI/CD | A merge to `main` produces images; `update.sh` deploys them locally |
| 4 | Observability and hardening | One trace spans API → NATS → worker; janitor, limits and alerts in place |
| 5 | Database grader | An EF Core problem grades on plan rules with SQL shown to the user |
| 6 | More graders | Refactoring, architecture, secure coding and test-writing problems live |
| 7 | Product polish | Progress, learning paths, plans and (sandbox) payments |

## Phase 0 — Recon and setup

- [ ] Read Katabench's Terms and Acceptable Use.
- [ ] Install REA (`npx rea-agents setup`) and run the recon prompts in the REA guide tab.
- [ ] Write `feature-map.md` and `api-contract.md` in your own words.
- [ ] Create the monorepo from the template; run `/bootstrap`; add Contracts, Worker, Sandbox.Runner projects.
- [ ] Create a Clerk development instance; note issuer, publishable key, webhook secret.
- [ ] Extend `CLAUDE.md` with the new conventions: worker has no DB access, contracts live in `src/Contracts`, hidden tests never leave the sandbox image.

## Phase 1 — Walking skeleton

- [ ] Replace template JWT issuing with Clerk validation (`azp` check included).
- [ ] Entities: `User`, `Problem`, `TestCase`; migrations; seed three free problems from `problems/`.
- [ ] Slices: `GetProblems`, `GetProblem`, `GetMe`, user upsert on first request.
- [ ] React app: Clerk sign-in, catalog page, editor page with Monaco and the statement.
- [ ] Astro site: home, catalog generated from `problems/`, links into the app.
- [ ] Compose: Traefik, Postgres, Redis, API, web, marketing; smoke check URLs.

## Phase 2 — Algorithms grading pipeline

- [ ] Entities: `Submission`, `SubmissionTestResult`, `OutboxMessage`.
- [ ] Slices: `CreateRun`, `CreateSubmission`, `GetSubmission`; rate limits and in-flight cap.
- [ ] NATS JetStream streams `JOBS` and `RESULTS`; outbox publisher; results consumer.
- [ ] Sandbox.Runner: Roslyn compile, collectible load context, measurement loop, nonce-delimited JSON output.
- [ ] Algorithms sandbox image; DinD service; worker consumer with Docker.DotNet and all hardening flags.
- [ ] Hostile-submission suite (network, infinite loop, 2 GB allocation, process spawning, filesystem writes, fake result).
- [ ] UI: Run/Submit, polling, per-test results with time and allocation budget bars, compiler errors as Monaco markers.

## Phase 3 — CI/CD

- [ ] `ci.yml` green on a PR (build, tests with Testcontainers, frontend lint/test/build).
- [ ] `images.yml` pushes four images tagged by SHA and `latest` to GHCR.
- [ ] `sandbox-images.yml` builds, runs the hostile suite, pushes.
- [ ] Branch protection on `main`; Dependabot; secret scanning.
- [ ] `deploy/update.sh` with SHA pinning and rollback tested once.

## Phase 4 — Observability and hardening

- [ ] Propagate W3C trace context through NATS message headers so one trace covers API → worker → result.
- [ ] Grafana dashboards: submissions per minute, grading latency (p50/p95), verdict mix, queue depth, sandbox failures.
- [ ] Janitor for stale sandbox containers; disk alert on the DinD volume.
- [ ] On Linux: try gVisor (`runsc`) for sandboxes and compare timings.
- [ ] REA audits of the web bundle and worker assembly added to CI.
- [ ] Restore a backup into a scratch database once, end to end.

## Phase 5 — Database and EF Core grader

- [ ] Database sandbox image with Postgres in `tmpfs` and a pre-seeded data directory.
- [ ] SQL-capturing `DbCommandInterceptor`; `EXPLAIN (FORMAT JSON)` per statement.
- [ ] Plan rules: `noSeqScan`, `usesIndex`, `maxRoundTrips`.
- [ ] UI: expandable SQL and plan summary per test.
- [ ] Five original EF Core problems (N+1, filter before fetch, sargable date ranges, projection, pagination).

## Phase 6 — More graders

- [ ] Refactoring: Roslyn metrics (complexity, nesting, method length, duplication) with thresholds per problem.
- [ ] Architecture: multi-file submissions, NetArchTest rules.
- [ ] Secure coding: adversarial payload suites.
- [ ] Test writing: user tests vs. correct implementation and planted mutants.

## Phase 7 — Product polish

- [ ] Progress, streaks and XP from `UserProblemProgress`.
- [ ] Learning paths: ordered problem lists with prerequisites.
- [ ] Plans and permissions (Free vs Pro tracks).
- [ ] Payments in sandbox mode: Paddle (merchant of record, handles global sales tax) or Stripe test mode (available in New Zealand; you handle tax registration yourself). Not tax advice; check before charging real money.
- [ ] Optional public preview through a free Cloudflare Tunnel, with Clerk switched to a production instance on your own domain.

## Working with Claude Code

Your template's skills and REA's MCP tools work side by side in one Claude Code session:

| Task | Use |
| --- | --- |
| New use case (command/query, validator, endpoint, tests) | `/add-feature` |
| New entity with EF configuration and migration | `/add-entity`, `/add-migration` |
| Outbox publisher, results consumer, janitor | `/add-background-job` |
| Review a branch against VSA conventions | `/vsa-review` |
| Check a built bundle or assembly for leaks or forbidden dependencies | REA (prompts in the REA guide tab) |

Record the big decisions as short ADRs in `docs/adr/`: Clerk over self-issued JWTs, DinD over the host socket, outbox over direct publish, polling before SSE.
