# Shared components

Built once in `apps/web/src/components/` from [tokens.css](tokens.css); the Astro site re-implements
the few it needs (Button, Badge, DifficultyBadge) as `.astro` components with the same classes.
Every component: named export, typed props, keyboard reachable, visible focus ring (FE-09, FE-10).

| Component | Variants / props | States | Notes |
| --- | --- | --- | --- |
| `Button` | `variant: primary \| secondary \| ghost \| danger`, `size: sm \| md`, `loading` | default, hover, focus-visible, disabled, loading (spinner + keeps width) | Primary = `bg-accent text-on-accent`; one primary per view |
| `Badge` | `tone: neutral \| accent \| pass \| fail \| warn` | — | Text + optional icon; never colour alone |
| `DifficultyBadge` | `difficulty: easy \| medium \| hard` | — | Neutral badge with 1–3 filled pips + word; difficulty is not a status, so no red/green |
| `TrackBadge` | `track` | — | Mono, lowercase (`algorithms`, `database`, …) |
| `VerdictBadge` | `verdict: Accepted \| WrongAnswer \| CompileError \| Timeout \| BudgetExceeded \| InternalError`, `status: Pending \| Running` | pending (spinner), running (spinner), final | Icon + label: ✓ Accepted (pass) · ✗ Wrong answer (fail) · ⚠ Budget exceeded (warn) · ⏱ Timeout (warn) · `{}` Compile error (fail) · ⓘ Internal error (neutral) |
| `BudgetBar` | `used`, `budget`, `unit: µs \| B` | under (pass), near ≥ 80 % (warn), over (fail) | Bar width = `min(used/budget, 1.25)` via inline style (FE-11 exception); label `412 µs / 1 000 µs` in `.tabular`; `role="meter"` with `aria-valuenow/max` |
| `Tabs` | controlled, `items[]` | selected, focus | WAI-ARIA tabs pattern; arrow keys move |
| `DataTable` | columns, rows, empty, loading | sortable header (`aria-sort`), row hover | TanStack Table in admin (3A); plain table elsewhere |
| `EmptyState` | title, body, action? | — | One sentence of what to do next |
| `Spinner` | `size` | — | `role="status"` + visually hidden label |
| `CodePane` | read-only code / SQL / plan JSON | — | `bg-surface-code`, mono, horizontal scroll, copy button |
| `ThemeToggle` | — | light, dark, system | Writes `localStorage.theme`, toggles `<html class="dark">` |
| `Kbd` | keys[] | — | Shows shortcuts (`Ctrl` `Enter`) |

## Monaco theme

`codebench-light` / `codebench-dark` via `monaco.editor.defineTheme`, derived from the tokens:
background `surface-code`, foreground `ink`, line numbers `ink-muted`, selection = accent at 25 %
alpha, cursor = accent, error squiggle = fail, warning squiggle = warn. Font = `--font-mono`, 14 px.
