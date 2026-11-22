# Shared components

Built once in `apps/web/src/components/` from [tokens.css](tokens.css); the Astro site re-implements
the few it needs (Button, Badge, DifficultyBadge) as `.astro` components with the same classes.
Every component: named export, typed props, keyboard reachable, visible focus ring (FE-09, FE-10).

**Implementation:** where a shadcn/ui primitive exists, the component wraps or re-skins it from
`src/components/ui/` (Radix supplies keyboard + ARIA behaviour) and takes its colours from the shadcn bridge
in `tokens.css` (FE-16). Variants are `cva` + `cn()` (FE-11). Icons are `lucide-react` named imports,
`aria-hidden` next to a visible word (FE-17). The look is defined **here**, not by shadcn's defaults.

| Component | Built on (shadcn/ui) | Icons (lucide-react) |
| --- | --- | --- |
| `Button` | `button` — variants re-skinned to the four below; `danger` = `bg-fail text-surface` | `LoaderCircle` (loading) |
| `Badge`, `DifficultyBadge`, `TrackBadge` | `badge` | — |
| `VerdictBadge` | `badge` | `CircleCheck` Accepted · `CircleX` Wrong answer · `TriangleAlert` Budget exceeded · `Timer` Timeout · `Braces` Compile error · `Info` Internal error · `LoaderCircle` Pending/Running |
| `BudgetBar` | own (`div` + `role="meter"`) | — |
| `Tabs` | `tabs` (Radix Tabs: WAI-ARIA tabs, arrow keys) | — |
| `DataTable` | `table` + TanStack Table (admin, 3A) | `ArrowUpDown`, `ArrowUp`, `ArrowDown` (sort) |
| `EmptyState` | own | optional context icon |
| `Spinner` | own | `LoaderCircle` (`animate-spin`, disabled by reduced motion) |
| `CodePane` | own (`bg-surface-code`) | `Copy`, `Check` |
| `ThemeToggle` | `dropdown-menu` + zustand `useThemeStore` | `Sun`, `Moon`, `Monitor` |
| `Kbd` | own | — |
| Dialogs, menus, tooltips, selects, inputs | `dialog`, `dropdown-menu`, `tooltip`, `select`, `input`, `label` | — |

| Component | Variants / props | States | Notes |
| --- | --- | --- | --- |
| `Button` | `variant: primary \| secondary \| ghost \| danger`, `size: sm \| md`, `loading` | default, hover, focus-visible, disabled, loading (spinner + keeps width) | Primary = `bg-accent text-on-accent`; one primary per view |
| `Badge` | `tone: neutral \| accent \| pass \| fail \| warn` | — | Text + optional icon; never colour alone |
| `DifficultyBadge` | `difficulty: easy \| medium \| hard` | — | Neutral badge with 1–3 filled pips + word; difficulty is not a status, so no red/green |
| `TrackBadge` | `track` | — | Mono, lowercase (`algorithms`, `database`, …) |
| `VerdictBadge` | `verdict: Accepted \| WrongAnswer \| CompileError \| Timeout \| BudgetExceeded \| InternalError`, `status: Pending \| Running` | pending (spinner), running (spinner), final | Icon + label (lucide, see table above): ✓ Accepted (pass) · ✗ Wrong answer (fail) · ⚠ Budget exceeded (warn) · ⏱ Timeout (warn) · `{}` Compile error (fail) · ⓘ Internal error (neutral) |
| `BudgetBar` | `used`, `budget`, `unit: µs \| B` | under (pass), near ≥ 80 % (warn), over (fail) | Bar width = `min(used/budget, 1.25)` via inline style (FE-11 exception); label `412 µs / 1 000 µs` in `.tabular`; `role="meter"` with `aria-valuenow/max` |
| `Tabs` | controlled, `items[]` | selected, focus | WAI-ARIA tabs pattern; arrow keys move |
| `DataTable` | columns, rows, empty, loading | sortable header (`aria-sort`), row hover | TanStack Table in admin (3A); plain table elsewhere |
| `EmptyState` | title, body, action? | — | One sentence of what to do next |
| `Spinner` | `size` | — | `role="status"` + visually hidden label |
| `CodePane` | read-only code / SQL / plan JSON | — | `bg-surface-code`, mono, horizontal scroll, copy button |
| `ThemeToggle` | — | light, dark, system | `useThemeStore` (zustand `persist`, key `codebench-theme`) toggles `<html class="dark">`; an inline script in `index.html` applies it before first paint |
| `Kbd` | keys[] | — | Shows shortcuts (`Ctrl` `Enter`) |

## Monaco theme

`codebench-light` / `codebench-dark` via `monaco.editor.defineTheme`, derived from the tokens:
background `surface-code`, foreground `ink`, line numbers `ink-muted`, selection = accent at 25 %
alpha, cursor = accent, error squiggle = fail, warning squiggle = warn. Font = `--font-mono`, 14 px.
