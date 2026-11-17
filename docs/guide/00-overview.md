# CodeBench — Build Guide (Katabench-style .NET SaaS)

Nov 11, 2026 · Archer Zou

## Purpose and scope

CodeBench is a learning project: a hands-on C# practice platform modelled on the publicly described design of [Katabench](https://katabench.com/), built end to end with your own name, challenges and branding. It runs entirely in Docker on your machine, with GitHub Actions for CI/CD and no paid hosting.

The working name **CodeBench** is a placeholder; rename it everywhere with the template's `/bootstrap` skill.

**What you are building (MVP):**

- A static marketing site (Astro) with a problem catalog, pricing page and blog.
- A web app (React + Vite + TypeScript) with a Monaco code editor, Run and Submit.
- Sign-in via Clerk (free dev instance).
- A .NET 10 API on your Vertical Slice Architecture template.
- An asynchronous grading pipeline: API → NATS JetStream → Worker → disposable sandbox container → results back.
- Graders for algorithms first, then EF Core/SQL query plans, refactoring metrics, architecture rules and test writing.
- Observability with OpenTelemetry → Grafana, Loki, Tempo, Prometheus (already in the template).

**Out of scope for now:** paid hosting, a public production deployment, and real payments (Paddle or Stripe sandbox only).

**Boundaries when studying Katabench:**

- Use REA only to *observe* the public site and the app through your own normal session. REA's own disclaimer limits it to software you own or are authorized to examine.
- Do not send crafted payloads at their sandbox, replay or modify their API calls, scrape puzzles, hidden tests or reference solutions, or copy text, logos or design assets.
- Read their [Terms](https://katabench.com/legal/terms) and [Acceptable Use](https://katabench.com/legal/acceptable-use) before any recon.
- Write every challenge, test and grader yourself. Their public descriptions ("allocation budgets", "graded query plans") are ideas, which you implement independently.

**Sources this guide draws on:** [Milan Jovanović — The System Design Behind My New SaaS](https://milanjovanovic.tech/blog/the-system-design-behind-my-new-saas) (Jul 2026), [REA repository](https://github.com/morluto/rea), and your [Vertical Slice Architecture Template](https://github.com/archerzou/Vertical_Slice_Architecture_Template).

## Target architecture

The system is three applications (marketing site, web app, API) plus an asynchronous grading pipeline, all as containers in one Compose project. Docker networks play the role of Milan's VPN ACLs: the browser reaches only Traefik, and sandboxes reach nothing.

```text
Browser ──sign-in──> Clerk (SaaS)
   │                    │ JWKS, webhooks
   v                    v
Traefik (ports 80/443 only)
   ├── Marketing  (Astro build in nginx)  codebench.localhost
   ├── Web app    (React build in nginx)  app.codebench.localhost
   └── API        (.NET 10, vertical slices) api.codebench.localhost
           │
           v
 ┌─ backend network ─────────────────────────────────────────────┐
 │  Postgres 17 (data + outbox) · Redis 7 (HybridCache L2)       │
 │  NATS JetStream (work queue) · Observability (OTel → Grafana) │
 └───────────────────────────────────────────────────────────────┘
           │ jobs in, grades out
           v
 ┌─ exec network (internal, no internet) ────────────────────────┐
 │  Worker (NATS only, no DB credentials)                        │
 │     └──> Sandbox daemon (Docker-in-Docker)                    │
 │           one throwaway container per run                     │
 │           no network, CPU, memory and time caps               │
 └───────────────────────────────────────────────────────────────┘
```

*(The doc shows this as a drawn diagram; export the Overview tab as PDF from the doc to get the picture.)*

The worker sits on the backend network only to reach NATS; it never receives the Postgres connection string. The sandbox daemon is a separate Docker engine on an internal network, so the worker never touches your host's Docker socket.

**A submission, end to end:**

1. The user presses **Submit** in the web app; the Clerk session token goes with the request.
2. The API validates the token, stores a `Pending` submission and an outbox row in one transaction, and returns `202 Accepted` with the submission id.
3. The outbox publisher sends `SubmissionCreated` to the JetStream subject `submissions.created`.
4. A Worker pulls the message (competing consumers: add workers to scale), asks the sandbox daemon to start a container from the right sandbox image, and streams the code in.
5. The grader harness compiles with Roslyn, runs visible and hidden tests, measures time, allocations, query plans or code metrics, and prints a JSON result.
6. The Worker publishes `SubmissionGraded` to `submissions.graded`; the container is removed.
7. The API consumes the result, updates the submission, awards XP, and invalidates cached progress.
8. The web app polls `GET /submissions/{id}` (later: Server-Sent Events) and renders per-test results.

**Run** follows the same path with visible tests only and a lower priority subject (`runs.created`), so quick feedback never queues behind heavy EF Core gradings.

## Tech stack and what changed from Milan's design

Everything Milan self-hosts on three Hetzner servers runs here as containers in one Docker Compose project, and GitHub Actions replaces Dokploy. Total running cost: $0 (Clerk, GitHub and Paddle sandbox free tiers).

| Concern | Katabench (per Milan's article) | CodeBench (this guide) | Cost |
| --- | --- | --- | --- |
| Marketing site | Astro on Cloudflare Pages | Astro, built to static files, served by an nginx container | $0 |
| App | React + Vite + TS on Cloudflare Pages | React + Vite + TS, static build in an nginx container | $0 |
| Auth | Clerk | Clerk development instance | $0 (free tier) |
| Payments | Paddle (merchant of record) | Paddle sandbox (or Stripe test mode) — later phase | $0 |
| API | .NET API | .NET 10 API on your VSA template | $0 |
| Database | Postgres on the API server | `postgres:17` container + named volume | $0 |
| Cache | Redis | `redis:7` container as `HybridCache` L2 | $0 |
| Queue | NATS JetStream | `nats:2` container with `-js` | $0 |
| Code execution | Worker + no-network sandbox on its own server | Worker container + a dedicated Docker-in-Docker daemon that only runs sandboxes | $0 |
| Reverse proxy | Traefik managed by Dokploy | Traefik container with Docker labels (`*.localhost` hostnames) | $0 |
| Private network | Headscale VPN + ACLs | Separate Docker networks (`edge`, `backend`, `exec`) as the "ACLs" | $0 |
| Deployments | Dokploy | GitHub Actions: build, test, no need to push images; `docker compose pull && up` locally | $0 for public repos; free minutes/storage for private |
| Observability | OTel agents → gateway → Grafana, Loki, Tempo, Prometheus | Same stack as containers (the template's compose already has it) | $0 |
| Backups | Nightly `pg_dump` to Cloudflare R2 | Nightly `pg_dump` to a local `./backups` folder via a cron container | $0 |

**Two honest trade-offs of running locally:**

- *Isolation is weaker than separate servers.* On Milan's setup, a sandbox escape lands on a worker box with no DB access. Locally, everything shares one machine. Mitigate with a dedicated Docker-in-Docker daemon for sandboxes, gVisor where available (Linux only), and never running untrusted code you didn't write until you're comfortable with the hardening in the *Worker & Sandbox* tab.
- *Docker Desktop on Windows/macOS cannot use gVisor (`runsc`).* Use WSL2 with a native Docker Engine if you want gVisor, or accept standard container isolation for local learning.

**Going public later, still free:** expose the stack through a [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/) (free) or move the two static frontends to Cloudflare Pages. Neither needs a paid server.

## Repository layout

One monorepo holds every deployable, so one `docker compose up` and one CI workflow cover the whole system.

```text
codebench/
├─ apps/
│  ├─ marketing/                 # Astro site (static) → nginx image
│  │  ├─ src/pages/              # index, pricing, problems/[slug], blog
│  │  ├─ src/content/            # problem summaries + blog posts (Markdown)
│  │  └─ Dockerfile
│  └─ web/                       # React + Vite + TS app → nginx image
│     ├─ src/features/           # problems, editor, submissions, account
│     ├─ src/lib/api.ts          # fetch wrapper that attaches the Clerk token
│     └─ Dockerfile
├─ src/
│  ├─ Web.Api/                   # from Vertical_Slice_Architecture_Template
│  │  ├─ Features/
│  │  │  ├─ Problems/            # GetProblems, GetProblem, (admin) CreateProblem
│  │  │  ├─ Submissions/         # CreateSubmission, RunCode, GetSubmission
│  │  │  ├─ Users/               # HandleClerkWebhook, GetMe
│  │  │  └─ Progress/            # GetProgress, streaks, XP
│  │  ├─ Messaging/               # Outbox publisher, graded-result consumer
│  │  └─ Dockerfile
│  ├─ Worker/                    # .NET worker service, NO database access
│  │  ├─ SubmissionConsumer.cs
│  │  ├─ SandboxRunner.cs        # talks to the sandbox Docker daemon
│  │  └─ Dockerfile
│  ├─ Contracts/                 # NATS message records shared by API + Worker
│  └─ Sandbox.Runner/            # grader harness baked into sandbox images
│     ├─ Graders/Algorithms/
│     ├─ Graders/Database/
│     └─ Graders/Refactoring/
├─ sandbox-images/
│  ├─ algorithms.Dockerfile
│  └─ database.Dockerfile        # adds a Postgres server inside the sandbox
├─ problems/                     # problem definitions: statement, starter code, visible + hidden tests
│  └─ two-sum/
│     ├─ problem.yaml
│     ├─ Starter.cs
│     ├─ Tests.Visible.cs
│     ├─ Tests.Hidden.cs
│     └─ Reference.cs
├─ tests/                        # Unit, Integration (Testcontainers), Architecture
├─ deploy/
│  ├─ docker-compose.yml         # full stack
│  ├─ docker-compose.dev.yml     # hot-reload overrides
│  ├─ traefik/
│  ├─ otel/                      # collector config
│  ├─ grafana/                   # provisioning: datasources, dashboards
│  └─ .env.example
├─ docs/                         # recon.md, feature-map.md, api-contract.md, ADRs
├─ .github/workflows/            # ci.yml, images.yml, sandbox-images.yml
├─ CLAUDE.md                     # conventions for Claude Code (extend the template's)
└─ CodeBench.slnx
```

Keep `problems/` as plain files in Git. A small seeding slice (or a CLI command) loads them into Postgres, and the sandbox image build copies hidden tests in, so hidden tests never travel through the browser.

## How to use this guide

Read the tabs in order once, then work from the Roadmap tab's checklists and dip into the others as reference.

| Tab | Read it for | When you need it |
| --- | --- | --- |
| 1 · REA guide | Installing REA, observation-only recon prompts, auditing your own builds | Phase 0, then before every release |
| 2 · Backend API | Clerk auth, data model, slices, NATS, outbox, caching, tests | Phases 1–2 |
| 3 · Worker & Sandbox | Threat model, Docker-in-Docker, sandbox images, grader harness, hardening | Phases 2, 4, 5, 6 |
| 4 · Frontend | Astro site, React app, Clerk in the browser, runtime config | Phase 1 onward |
| 5 · Docker local stack | Full `docker-compose.yml`, `.env`, dev overrides, commands, troubleshooting | Every day |
| 6 · CI/CD | GitHub Actions workflows, GHCR, local deploy and rollback | Phase 3 onward |
| 7 · Roadmap | Phase-by-phase checklists and Claude Code workflow | Throughout |
| 8 · Config & secrets | Every third-party API key (Clerk, GitHub, Paddle/Stripe, Cloudflare), full .env.example, per-service variables, user-secrets, Actions secrets | Before first run, and when adding a provider |
| 9 · Seed data & admin | Six original seed problems, data-driven tests, admin role bootstrap, admin API and /admin console (CRUD, validate/publish, users, dashboard) | Phase 1 (seeding), then after Phase 3 (admin console) |

Code in this guide is a starting point written against .NET 10, NATS.Net v2, Docker.DotNet and Clerk as of the date above. Library APIs move; check each package's current docs when you install it, and let the compiler and tests have the final word.
