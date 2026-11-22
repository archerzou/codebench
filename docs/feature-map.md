# Feature map

What CodeBench builds, by phase. Designed from our own requirements — no third-party product was
observed (see [recon.md](recon.md), [ADR 0006](adr/0006-no-katabench-recon.md)). The "Source" column
says where each feature is specified. Phases follow [guide/07-roadmap.md](guide/07-roadmap.md)
(admin console slots in between Phases 3 and 4).

| Feature | Source | CodeBench version | Phase |
| --- | --- | --- | --- |
| Sign-in | guide 02 §2, ADR 0001 | Clerk dev instance (email + GitHub); API validates Clerk JWT incl. `azp` | 1 |
| Problem catalog | guide 02 §5, 04 | `GET /problems` with track/difficulty/search, paged + cached; Astro SEO pages | 1 |
| Editor | guide 04 | Monaco, statement pane, budgets shown up front, drafts in `localStorage` | 1 |
| Seed problems | guide 09, ADR 0005 | 6 original problems in `problems/*.yaml`, loaded insert-only by `--seed`; the 4 algorithms problems now, `customers-since` / `shipping-cost-refactor` automatically once their graders exist; DB is source of truth after seeding | 1 (5, 6) |
| Roles + bootstrap admin | guide 09 | learner / author / admin in our DB; `Admin:BootstrapEmails` | 1 |
| Try before signup | guide 02 §5 | Anonymous Run on free problems, rate-limited per IP | 2 |
| Run / Submit | guide 02 §5–7, ADR 0003 | `POST /runs` (visible tests) and `POST /submissions` (all tests) → 202, outbox → NATS | 2 |
| Per-test results | guide 03, 04 | Pass/fail, median time (µs), allocated bytes, budget bars; hidden tests show name + pass only | 2 |
| Compile errors | guide 03, 04 | Roslyn diagnostics mapped to Monaco markers | 2 |
| Result delivery | ADR 0004 | Polling every 750 ms until Graded/Failed; SSE later if needed | 2 |
| Sandboxed grading | guide 03, ADR 0002 | DinD daemon, one no-network container per run, data-driven harness | 2 |
| CI/CD | guide 06 | `ci.yml`, `images.yml`, `sandbox-images.yml`, local `update.sh` deploy | 3 |
| Admin console | guide 09 | `/admin`: problems CRUD, tests grid, Validate → Publish, users, submissions, audit | 3A |
| Tracing across the queue | guide 05, 07 §4 | W3C trace context in NATS headers, Grafana dashboards | 4 |
| Self-audits (REA) | guide 01 "own builds" | Web bundle + Worker assembly audits in CI | 4 |
| Query-plan grading | guide 03 | EF Core problems; `EXPLAIN (FORMAT JSON)` rules: noSeqScan, usesIndex, maxRoundTrips | 5 |
| Refactoring grading | guide 03 | Roslyn metrics (complexity, nesting, length, duplication) + behaviour tests | 6 |
| Architecture / secure coding / test writing | guide 03 | NetArchTest rules; adversarial suites; mutant-killing user tests | 6 |
| Progress, streaks, XP | guide 07 §7 | `UserProblemProgress`, `GET /me/progress` | 7 |
| Learning paths | guide 07 §7 | Ordered problem lists with prerequisites | 7 |
| Plans + payments | guide 07 §7, 08 | Free vs Pro permissions; Paddle sandbox or Stripe test mode | 7 |
