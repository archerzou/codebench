---
name: add-astro-page
description: Add a static page or a content-collection entry (blog post, legal page, how-it-works section) to the CodeBench Astro marketing site in apps/marketing. Use when the user wants marketing, landing, pricing, blog, or legal content.
argument-hint: <page or post, e.g. "blog post: why allocation budgets matter">
---

# Add an Astro Page

Guide 04 "Marketing site"; rule FE-13 in `context/code_rules.md`.

## Rules

- `output: 'static'` stays. No server endpoints, no auth, no API calls at runtime.
- **Never** hand-edit `src/content/problems/*` — it is generated from `problems/*/problem.yaml` before
  build and contains summaries only. Never put tests, reference solutions or hidden details on the site.
- Copy is your own — nothing copied from Katabench (text, layout, illustrations).
- Links into the app use `import.meta.env.PUBLIC_APP_URL` and carry `?entry=<page-slot>` for attribution.

## Workflow

1. **Choose the form**
   - Page with layout/components → `src/pages/<route>.astro`.
   - Long-form text (how-it-works, legal) → `src/pages/<route>.mdx`.
   - Blog post → `src/content/blog/<slug>.md(x)` with the collection's frontmatter (title, description,
     pubDate, tags); it appears in `/blog`, the RSS feed and the sitemap automatically.
2. **Reuse** the base layout and shared components (`src/layouts/`, `src/components/`); set `<title>` and
   meta description per page.
3. **Style** with Tailwind v4 and the shared tokens — colour **roles** only (`bg-surface`, `text-ink-muted`,
   `bg-primary`), never raw palette classes; reuse the `.astro` Button/Badge/DifficultyBadge (classes mirror
   the app's variants, `docs/design/components.md`). Icons: inline SVG from `lucide-static`
   (`?raw` + `set:html`, `aria-hidden="true"` beside visible text). No React islands, no client framework
   (ADR 0007). Semantic headings in order, alt text on images; copy follows `docs/design/brief.md` voice.
4. **Verify**
   ```powershell
   npm --prefix apps/marketing run build      # must succeed; check dist/ for the new route
   npm --prefix apps/marketing run dev        # http://localhost:4321
   ```

## Done when

Build succeeds, the page renders at its route, links into the app work and carry `?entry=`, and nothing
from `problems/` beyond summaries is published.
