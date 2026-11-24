# Wireframe — editor (`/problems/:slug` in the app)

Low fidelity. Data: `GET /problems/{slug}` (statement, starter code, signature, budgets, visible
tests, `hiddenTestCount`); results from `GET /submissions/{id}` (Phase 2).

## ≥ 72 rem (desktop) — two resizable columns, results drawer under the code

```text
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [›▪]codebench Problems                                         ◐ theme   [ avatar ]      │
├───────────────────────────────────┬──────────────────────────────────────────────────────┤
│ ‹ Problems                        │  Solution.cs                    ⟲ Reset   ⌘↵ Run  [ Submit ] │  toolbar
│                                   ├──────────────────────────────────────────────────────┤
│ Sum of evens                      │   1  public sealed class Solution                    │
│ algorithms · ●○○ easy · FREE      │   2  {                                               │
│                                   │   3      public int SumOfEvens(int[] nums)           │  Monaco
│ ┌ Budgets ──────────────────────┐ │   4      {                                           │  (codebench theme)
│ │ time     50 ms per test       │ │   5          // your code                            │
│ │ alloc    1 KB per test        │ │   6      }                                           │
│ │ limits   10 s · 256 MB        │ │   7  }                                               │
│ └───────────────────────────────┘ │                                                      │
│                                   │                                                      │
│ Statement (markdown)              ├──────────────────────────────────────────────────────┤
│ …                                 │ Results            ✓ Accepted · 7/7 · 412 µs median  │  drawer header
│                                   │ ──────────────────────────────────────────────────── │  aria-live=polite
│ Examples (visible tests)          │ ✓ empty array         3 µs  [■□□□□□□□□]   0 B / 1 KB │
│ ┌───────────────────────────────┐ │ ✓ mixed signs        11 µs  [■■□□□□□□□]   0 B / 1 KB │  BudgetBar ×2
│ │ nums = [1, 2, 3, 4]  →  6     │ │ ✗ large input       780 µs  [■■■■■■■■■] 4.1 KB/1 KB  │  over → fail
│ └───────────────────────────────┘ │ ✓ hidden test 1        —   hidden                    │  hidden: name+pass only
│ + 5 hidden tests                  │                                                      │
│                                   │                                                      │
└───────────────────────────────────┴──────────────────────────────────────────────────────┘
   statement column 40 %, min 22 rem          code column 60 %; drawer 35 % of height, collapsible
```

## < 72 rem

Stacked: statement (collapsible, collapsed after first Run) → editor → results. Run/Submit move to a
sticky bottom bar.

## States

| State | Results drawer |
| --- | --- |
| Phase 1 (no grading yet) | Drawer hidden; Run/Submit disabled with tooltip "Grading arrives in Phase 2" — or not rendered |
| Idle | "Run checks your code against the examples. Submit runs every test, including hidden ones." |
| Pending / Running | `VerdictBadge` spinner + "Queued…" / "Running 7 tests…"; buttons disabled (FE-07) |
| Compile error | `{}` Compile error · list of diagnostics `Line 12, col 9 — CS0165 …`; click → jumps to Monaco marker |
| Graded | Verdict summary + per-test rows; failing rows first |
| Failed (ours) | ⓘ "Grading failed on our side…" + Retry |
| Signed out + Submit | Store code in `sessionStorage`, open Clerk sign-in, resubmit on return (FE-07) |

## Behaviour

- Draft autosaves to `localStorage` key `draft:{slug}` (debounced 500 ms); "Reset" restores starter
  code after a confirm dialog (focus returns to the editor).
- Ctrl/Cmd+Enter = Run, Ctrl/Cmd+Shift+Enter = Submit (shown with `Kbd`).
- Budgets shown before any run (principle 5). Hidden tests never show args/expected (QY-04).
- Column split remembered in `localStorage`.
