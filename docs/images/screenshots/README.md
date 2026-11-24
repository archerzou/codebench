# Screenshots

Key pages of CodeBench's own apps, captured from the local compose stack (`--profile app`, through Traefik) at
1440 × 900 (mobile: 390 × 844), 1× scale. Referenced from [apps/web/README.md](../../../apps/web/README.md) and
[apps/marketing/README.md](../../../apps/marketing/README.md). Only our own product — no third-party captures
(those stay out of the repo, ADR 0006).

**Names:** `{app}-{page}[-{state}]-{theme}.png`, kebab-case (NM-12) — `app` is `web` or `marketing`, `theme` is
`light` or `dark`, e.g. `web-catalog-filtered-light.png`, `marketing-mobile-menu-light.png`.

| File | Shows |
| --- | --- |
| `web-catalog-light.png`, `web-catalog-dark.png` | App catalog: search, track / difficulty / sort filters, Free / Pro badges |
| `web-catalog-filtered-light.png` | URL-driven filters (`?difficulty=medium&sort=title`) |
| `web-catalog-search-empty-light.png` | Empty state with "Clear filters" |
| `web-catalog-mobile-light.png` | Catalog below 72 rem (filters collapse into a dialog) |
| `web-editor-light.png`, `web-editor-dark.png` | Editor: statement, budgets, examples, hidden-test count, Monaco with the CodeBench theme |
| `web-sign-in-light.png` | Clerk sign-in themed with our tokens |
| `web-not-found-light.png` | 404 page |
| `marketing-home-light.png` (full page), `marketing-home-dark.png` | Home: hero, example result, what gets measured, tracks, pricing teaser |
| `marketing-problems-light.png` | Catalog generated from `problems/*.yaml` (summaries only) |
| `marketing-problem-detail-light.png` | SEO problem page with limits, test counts, "Solve in the editor" |
| `marketing-how-it-works-light.png` (full page) | Sandbox, measures, hidden tests, verdicts |
| `marketing-pricing-light.png` | Free vs Pro, by-track table |
| `marketing-blog-post-light.png` | Blog post layout |
| `marketing-mobile-menu-light.png` | No-JS `<details>` mobile menu, open |

**Retaking them:** start the stack (`docker compose -f deploy/docker-compose.yml --env-file deploy/.env
--profile app up -d --build`), then capture each URL with a headless browser (e.g. Playwright:
`page.goto(url)` → `page.screenshot({ path })`, `colorScheme: 'dark'` for the dark variants). Keep the file names so
the READMEs stay valid; retake after visible UI changes.
