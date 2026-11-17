# apps/marketing — Astro static site (`codebench.localhost`)

Phase 1. See [docs/guide/04-frontend.md](../../docs/guide/04-frontend.md).

Scaffold (run from `apps/`; move this README aside first):

```bash
npm create astro@latest marketing -- --template minimal --typescript strict
cd marketing
npx astro add tailwind sitemap mdx
```

Keep `output: 'static'`. The problem catalog (`src/content/problems/*.md`) is generated from
`problems/*/problem.yaml` by a small Node script before `astro build`, summaries only, never tests.
