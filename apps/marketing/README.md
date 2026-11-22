# apps/marketing — Astro static site (`codebench.localhost`)

Phase 1. See [docs/guide/04-frontend.md](../../docs/guide/04-frontend.md) "Marketing site".

Scaffold (run from `apps/`; move this README aside first):

```bash
npm create astro@latest marketing -- --template minimal --typescript strict
cd marketing
npx astro add tailwind sitemap mdx     # Astro >= 5.2: Tailwind v4 via @tailwindcss/vite (not @astrojs/tailwind)
npm i -D lucide-static yaml
```

- Keep `output: 'static'`; no client framework (no React islands — ADR 0007).
- `src/styles/global.css`: `@import "tailwindcss";` + `@import "../../../../docs/design/tokens.css";` — the
  same tokens as `apps/web`, never copied.
- The problem catalog (`src/content/problems/*.md`, git-ignored) is generated from `problems/*/problem.yaml`
  by `scripts/gen-problems.mjs` before `astro build`/`astro dev` — summaries only, never tests (FE-13).
- Docker: build context is this folder; `docs/design` and `problems` come in as additional build contexts
  (`deploy/docker-compose.yml`).
