# 7. Frontend UI stack: Tailwind + shadcn/ui + lucide-react + zustand

Status: accepted · 2026-11-22

## Context
`apps/web` was planned as React 19 + Vite + TypeScript with Tailwind v4, zod and TanStack Query, and every
shared component in `docs/design/components.md` hand-built. Dialogs, menus, selects, tooltips and tabs are
easy to get subtly wrong for keyboard and screen-reader users (FE-10), and the editor and catalog need a little
client-only state (theme, pane layout, per-problem drafts) that doesn't belong in TanStack Query.

## Decision
- **shadcn/ui** for interactive primitives: Radix-based components copied into `src/components/ui/` and
  owned by us (FE-16). They are re-skinned to `docs/design/components.md`; their CSS variables map onto our
  colour roles through the shadcn bridge in `docs/design/tokens.css`. shadcn's default palettes are not used.
- **lucide-react** for icons, named imports only (FE-17).
- **zustand** for client UI state only — never API data (TanStack Query, FE-03), never auth tokens (FE-15).
- TypeScript strict, Tailwind v4 and zod stay as before.
- **Astro marketing site — same design, no React:** Tailwind v4 through `@tailwindcss/vite` (not
  `@astrojs/tailwind`, which is v3-only), the same `docs/design/tokens.css` imported by relative path (the
  shadcn bridge makes `bg-primary`, `text-muted-foreground` … identical in both apps), `.astro` Button / Badge /
  DifficultyBadge whose classes mirror the app's `cva` variants, the same Lucide icons via `lucide-static`
  inlined at build time, dark mode from `prefers-color-scheme`. No React islands, so no shadcn/ui, zustand or
  lucide-react on the site, and it ships no client framework.

## Consequences
- **REA self-audit (Appendix D): no conflict.** All four libraries are client-side code with no secrets or
  network calls of their own; RA-05 (no secrets, no source maps, admin only in lazy chunks) is unaffected. RA-05
  additionally fails on `__REDUX_DEVTOOLS_EXTENSION__` in `dist/`, because zustand's `devtools` middleware must
  be dev-only. Named lucide imports keep the entry chunk small; admin-only shadcn components are pulled in only
  by lazy admin chunks.
- **ADR 0006: no conflict.** shadcn/ui is an open-source component kit, not another product's design; the
  visual identity still comes from `docs/design/`.
- **Naming:** `components/ui/` keeps shadcn's kebab-case, multi-export files so `npx shadcn add` upgrades stay
  diffable — recorded as KD-07 in `context/code_rules.md`.
- **Name clash:** shadcn's `accent` (subtle hover) ≠ our `accent` (teal primary). Generated hover classes are
  rewritten to `bg-surface-sunken text-ink` when a component is added.
- New dependencies to keep updated (Dependabot `npm`): `@radix-ui/*`, `class-variance-authority`, `clsx`,
  `tailwind-merge`, `tw-animate-css`, `lucide-react`, `zustand`.
