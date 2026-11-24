# Wireframe — catalog (`/` in the app)

Low fidelity. Boxes are regions, not pixels. Data comes from `GET /problems` (`PagedList<ProblemSummary>`).

## ≥ 72 rem (desktop)

```text
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ [›▪]codebench Problems                                         ◐ theme   [ Sign in ]     │  header 48px
├──────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│  Problems                                                      6 problems                │  h1 + count
│  Practise the parts that matter in production: speed, memory, SQL, readability.          │  ink-muted
│                                                                                          │
│  ┌ Search problems… ──────────────────┐  Track [ all ▾ ]  Difficulty [ all ▾ ]  Sort [ recommended ▾ ] │
│  └────────────────────────────────────┘                                                  │  filters → URL query
│                                                                                          │
│  ┌────────────────────────────────────────────────────────────────────────────────────┐  │
│  │ ✓  Sum of evens            algorithms   ●○○ easy   time 50 ms · alloc 1 KB   FREE │  │  row = link
│  │    Add up the even numbers without allocating.                                     │  │  summary, 1 line
│  ├────────────────────────────────────────────────────────────────────────────────────┤  │
│  │    Merge booking windows    algorithms   ●●○ medium time 200 ms · alloc 64 KB FREE │  │
│  │    Collapse overlapping ranges in one pass.                                         │  │
│  ├────────────────────────────────────────────────────────────────────────────────────┤  │
│  │ 🔒 Customers who ordered…   database     ●●○ medium plan rules: no seq scan    PRO  │  │  Pro: lock icon + word
│  │    Fetch recent customers with one indexed query.                                   │  │
│  └────────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                          │
│                         ‹ Prev    Page 1 of 1    Next ›                                  │  pagination
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

## < 72 rem

Filters collapse into a single "Filters (2)" button opening a sheet; rows become two-line cards
(title + badges on line 1, summary + budgets on line 2).

## Behaviour

- Filters and page live in the URL (`?track=algorithms&difficulty=easy&page=2`) → shareable, back works.
- Search debounced 250 ms; query key `['problems', filters]` (FE-03).
- ✓ solved marker only from Phase 7 (`solved?` in the summary); hidden until then — no empty column.
- Loading: 6 skeleton rows. Empty: `EmptyState` "No problems match these filters" + "Clear filters".
- Error: inline banner with the ProblemDetails `title`, "Retry" button.
- Row is one `<a>`; badges are text inside it (no nested interactive elements).
