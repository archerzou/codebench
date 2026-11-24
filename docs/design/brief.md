# CodeBench design brief

Original design for CodeBench. Derived from [feature-map.md](../feature-map.md) and the
[API contract](../api-contract.md) — not from screenshots of any other product (ADR 0006).
This brief, [tokens.css](tokens.css), [pages.md](pages.md), [components.md](components.md) and
[wireframes/](wireframes/) are the contract for `apps/web` and `apps/marketing`.

## Who it's for

C# developers who already pass interviews-style puzzles and want to get better at the parts that
matter in production: **how fast, how much memory, what SQL, how readable**. They live in an IDE
all day; they read numbers, not confetti.

## Principles

1. **Numbers first.** Elapsed µs, allocated bytes and budget usage are the hero of every result,
   set in tabular mono figures and shown next to their budget. A pass that blows the allocation
   budget is visibly not a pass.
2. **Calm editor.** The editor page is a workspace, not a landing page: neutral surfaces, one accent,
   no motion except state changes. Nothing competes with the code.
3. **Honest feedback.** Every state says what happened and what to do next — compile errors point at
   a line, hidden tests say *that* they failed without leaking *why*, timeouts say they were timeouts.
4. **Keyboard first.** Everything reachable by keyboard; Run = Ctrl/Cmd+Enter, Submit =
   Ctrl/Cmd+Shift+Enter; visible focus rings on every interactive element.
5. **Budgets up front.** Constraints and budgets are visible before you write a line, not discovered
   after a failure.

## Voice & tone

Precise, plain, a little dry. Second person, present tense, short sentences. Explain the *why* of a
budget once, then get out of the way.

| Instead of | Write |
| --- | --- |
| "Oops! Something went wrong 😢" | "Grading failed on our side. Your code wasn't the problem — run it again." |
| "Awesome job!!! 🎉" | "Accepted · 7/7 tests · 412 µs median · 0 B allocated" |
| "Error" | "Line 12: `nums` is never assigned (CS0165)" |

No exclamation marks in product UI. Marketing copy may be warmer, never hype.

## Identity

- **Mark & wordmark:** a prompt chevron `›` and a block cursor on an accent tile — a cursor waiting for
  input — followed by lowercase `codebench` in the UI mono font. The mark is also the favicon and app icon.
  Geometry, colours, files and usage rules: [brand/README.md](brand/README.md); one `<Wordmark />` in each app.
- **Accent:** deep teal (light) / bright teal (dark). Used for primary actions, links, focus rings,
  and the brand mark — nowhere else.
- **Status colours** (pass green, fail red, warn amber) are reserved for results and always paired
  with an icon and a word (FE-10).
- **Type:** system UI sans for interface text; a monospace face (JetBrains Mono, OFL — self-hosted
  later; system mono fallback) for code, numbers, slugs and the wordmark.
- **Shape:** small radii (4–10 px), 1 px borders, shadows only on floating layers (menus, dialogs).
- **Imagery:** no stock photos or illustrations. Marketing shows the real product: code, a results
  table, a budget bar.

## Accessibility baseline

- Every text/background pair in `tokens.css` ≥ 4.5:1 (lowest measured: 5.59); control borders and
  focus rings ≥ 3:1. Ratios are recorded in `tokens.css`.
- Light and dark themes from day one; respect `prefers-color-scheme`, allow manual override.
- `prefers-reduced-motion` disables all non-essential transitions.
