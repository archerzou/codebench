# Feature map

What CodeBench builds, by phase. The middle column is filled in from [recon.md](recon.md), in your own words.
Phases follow [guide/07-roadmap.md](guide/07-roadmap.md) (admin console slots in between Phases 3 and 4).

| Feature | Observed on Katabench (own words) | CodeBench version | Phase |
| --- | --- | --- | --- |
| Sign-in | _recon_ | Clerk dev instance (email + GitHub); API validates Clerk JWT incl. `azp` | 1 |
| Problem catalog | _recon_ | `GET /problems` with track/difficulty/search, paged + cached; Astro SEO pages | 1 |
| Editor | _recon_ | Monaco, statement pane, budgets shown up front, drafts in `localStorage` | 1 |
| Seed problems | – | 6 original problems from `problems/*.yaml`; DB is source of truth after seeding | 1 |
| Roles + bootstrap admin | – | learner / author / admin in our DB; `Admin:BootstrapEmails` | 1 |
| Try before signup | _recon_ | Anonymous Run on free problems, rate-limited per IP | 2 |
| Run / Submit | _recon_ | `POST /runs` (visible tests) and `POST /submissions` (all tests) → 202, outbox → NATS | 2 |
| Per-test results | _recon_ | Pass/fail, median time (µs), allocated bytes, budget bars; hidden tests show name + pass only | 2 |
| Compile errors | _recon_ | Roslyn diagnostics mapped to Monaco markers | 2 |
| Sandboxed grading | – | DinD daemon, one no-network container per run, data-driven harness | 2 |
| CI/CD | – | `ci.yml`, `images.yml`, `sandbox-images.yml`, local `update.sh` deploy | 3 |
| Admin console | – | `/admin`: problems CRUD, tests grid, Validate → Publish, users, submissions, audit | 3½ |
| Tracing across the queue | – | W3C trace context in NATS headers, Grafana dashboards | 4 |
| Query-plan grading | _recon_ | EF Core problems; `EXPLAIN (FORMAT JSON)` rules: noSeqScan, usesIndex, maxRoundTrips | 5 |
| Refactoring grading | _recon_ | Roslyn metrics (complexity, nesting, length, duplication) + behaviour tests | 6 |
| Architecture / secure coding / test writing | _recon_ | NetArchTest rules; adversarial suites; mutant-killing user tests | 6 |
| Progress, streaks, XP | _recon_ | `UserProblemProgress`, `GET /me/progress` | 7 |
| Learning paths | _recon_ | Ordered problem lists with prerequisites | 7 |
| Plans + payments | _recon_ | Free vs Pro permissions; Paddle sandbox or Stripe test mode | 7 |
