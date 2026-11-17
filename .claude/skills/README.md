# Vertical Slice Architecture Agent Skills for Claude Code

A skill pack that teaches Claude Code the conventions of the Vertical Slice Architecture template — so every feature it builds looks like you wrote it: one file per use case, custom command/query handlers (no MediatR), Result-based error handling, direct `ApplicationDbContext` access, minimal API endpoints, and full test coverage.

## What's inside

| Skill | Invoke with | What it does |
|---|---|---|
| **add-feature** | `/add-feature archive a todo item` | Scaffolds a complete vertical slice in one file: nested command/query, handler, validator, endpoint, plus unit + validator + integration tests. |
| **add-entity** | `/add-entity Project with a name and owner` | Adds a domain entity end to end: entity, error catalog, domain events, EF configuration, DbContext wiring, migration. |
| **add-tests** | `/add-tests CreateTodo` | Backfills handler, validator, and integration tests for existing use cases. |
| **add-migration** | `/add-migration Add_Projects` | Creates and applies an EF Core migration (single project — `src/Web.Api` is both migrations and startup project). |
| **add-background-job** | `/add-background-job delete expired refresh tokens hourly` | Adds a scheduled `BackgroundService` that safely resolves scoped services per run. |
| **bootstrap** | `/bootstrap Acme.Billing` | Renames the solution to your app and strips the sample Todos feature (keeps Users + auth). |
| **vsa-review** | `/vsa-review` | Reviews pending changes against the template's conventions: one-file slices, slice isolation, error handling, security, caching, and test coverage. |

### CodeBench skills

| Skill | Invoke with | What it does | Guide tab |
|---|---|---|---|
| **add-problem** | `/add-problem longest equal run, easy` | Writes `problems/<slug>/problem.yaml` (statement, signature, starter, reference, visible + hidden tests, budgets), then seeds/validates. | 9 |
| **add-grader** | `/add-grader refactoring metrics` | New grader kind: runner code, sandbox image, worker/compose/CI wiring, hostile cases, UI details. | 3 |
| **add-react-feature** | `/add-react-feature bookmarks` | Feature folder in `apps/web`: zod types, TanStack Query hooks via `useApi`, components, MSW-mocked tests. | 4 |
| **add-admin-crud** | `/add-admin-crud learning paths` | Admin slices + `/admin` page (table, form, permission guard, audit, 403/409 tests). | 9 |
| **add-astro-page** | `/add-astro-page blog: allocation budgets` | Static marketing page or content-collection entry. | 4 |
| **add-compose-service** | `/add-compose-service cloudflared` | Compose service with the right networks, profile, env, `.env.example`, Traefik labels. | 5, 8 |
| **run-hostile-suite** | `/run-hostile-suite algorithms` | Builds a sandbox image and runs the hostile submissions with the worker's exact flags. | 3, 6 |

You don't have to invoke them explicitly — once installed, Claude Code picks the right skill automatically when you say things like "add an endpoint to snooze a todo."

## Installation

The skills live in `.claude/skills/`. If you cloned the template, they're already active — just open the repo in Claude Code.

To use them in another project based on this template, copy the folder:

```
your-project/
└── .claude/
    └── skills/
        ├── add-feature/
        ├── add-entity/
        ├── add-tests/
        ├── add-migration/
        ├── add-background-job/
        ├── bootstrap/
        └── vsa-review/
```

Works with both the standard and the Aspire variants of the template.

## Try it

```
/add-feature snooze a todo until a given date
```

Claude will create the one-file slice — the command, validator, handler (with ownership check, domain event, and cache invalidation), and the endpoint — plus the three test types, then build and run the tests.

## Customizing

Each skill is a plain Markdown file (`SKILL.md`, plus templates under `references/`). Prefer a different folder layout, records everywhere, a different test stack? Edit the templates once and every future feature follows suit. The skills are the executable version of your team's conventions doc.

---

Built for the [Vertical Slice Architecture template](https://www.milanjovanovic.tech) by Milan Jovanović.
