# CodeBench

A hands-on C# practice platform (learning project): a problem catalog, a Monaco editor with **Run** and
**Submit**, and an asynchronous grading pipeline that compiles user code with Roslyn inside a disposable,
no-network sandbox container and reports per-test time, allocations, query plans or code metrics.

Everything runs locally in Docker; GitHub Actions does CI/CD. Built on the
[Vertical Slice Architecture template](https://github.com/archerzou/Vertical_Slice_Architecture_Template).

```text
Browser ─> Traefik ─┬─ marketing (Astro)   codebench.localhost
                    ├─ web (React + Vite)  app.codebench.localhost
                    └─ api (.NET 10)       api.codebench.localhost
                             │ outbox → NATS JetStream (jobs.*)
                             v
                  worker (no DB) ─> sandbox-daemon (DinD) ─> one sandbox per run
                             │ NATS (results.graded)
                             v
                    api updates submission, progress, cache
```

## Documentation

| | |
| --- | --- |
| Build guide | [docs/guide/00-overview.md](docs/guide/00-overview.md) → tabs 01–09 |
| Roadmap / checklists | [docs/guide/07-roadmap.md](docs/guide/07-roadmap.md) |
| Decisions | [docs/adr/](docs/adr/) |
| API design | [docs/api-contract.md](docs/api-contract.md) |
| Features by phase | [docs/feature-map.md](docs/feature-map.md) |
| Build plan / rules / progress | [context/build_plan.md](context/build_plan.md) · [context/code_rules.md](context/code_rules.md) · [context/progress_tracker.md](context/progress_tracker.md) |
| Conventions for Claude Code | [CLAUDE.md](CLAUDE.md) |

## Layout

| Path | What |
| --- | --- |
| `apps/marketing`, `apps/web` | Astro site, React app (Phase 1) |
| `src/Web.Api` | API, one file per use case under `Features/` |
| `src/Contracts` | NATS message records (BCL only) |
| `src/Worker` | grading orchestrator, no database access |
| `src/Sandbox.Runner` | grader harness baked into sandbox images |
| `sandbox-images/` | one Dockerfile per grader kind |
| `problems/` | six original problems (YAML seed data) |
| `tests/` | unit, integration (Testcontainers), architecture, hostile submissions |
| `deploy/` | Compose stack, dev overrides, `.env.example` |

## Getting started

Requirements: .NET 10 SDK, Docker (WSL2 backend on Windows, ≥ 8 GB RAM), Node.js 24.

```bash
cp deploy/.env.example deploy/.env          # fill in passwords (codebench/codebench for local dev) and Clerk values

# infra in Docker (Postgres, Redis, NATS, Traefik, Grafana stack), ports on 127.0.0.1
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d

dotnet build CodeBench.slnx
dotnet test CodeBench.slnx                  # Docker must be running (Testcontainers)
dotnet watch --project src/Web.Api          # migrations apply on start in Development
```

Compose profiles add the rest as phases land: `--profile app` (api, web, marketing), `--profile grading`
(worker, sandbox-daemon), `--profile backup` (nightly `pg_dump`).

## Status

Phase 0 (setup): template bootstrapped, monorepo laid out, contracts and project skeletons in place.
Still open: REA recon (`docs/recon.md`), Clerk dev instance. Next: Phase 1 walking skeleton — see the roadmap.
