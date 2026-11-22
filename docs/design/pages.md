# Page inventory

Every screen CodeBench ships, with its job and its one primary action. Built from
[feature-map.md](../feature-map.md); a new screen gets a row here (and a wireframe) before code.

## Marketing site — `codebench.localhost` (Astro, static)

| Route | Purpose | Primary action | Phase | Wireframe |
| --- | --- | --- | --- | --- |
| `/` | Say what CodeBench grades that others don't (time, allocations, SQL, readability); show a real result | "Try a free problem" → app, `?entry=home-hero` | 1 | — (when 1.9 starts) |
| `/problems` | Browsable catalog: track, difficulty, tags, one-line summary | Open a problem page | 1 | reuse catalog layout |
| `/problems/[slug]` | SEO page: title, track, difficulty, statement excerpt, budgets | "Solve in the editor" → app, `?entry=problem-page` | 1 | — |
| `/how-it-works` | Sandbox, budgets, median-of-7 timing, hidden tests — explained plainly | "Try a free problem" | 1 | — |
| `/pricing` | Free vs Pro tracks | "Start free" | 1 (copy), 7 (checkout) | — |
| `/blog`, `/blog/[slug]` | Articles + RSS | Read / subscribe via RSS | 1 | — |
| `/legal/*` | Terms, privacy, acceptable use (our own) | — | 1 | — |

## Web app — `app.codebench.localhost` (React)

| Route | Purpose | Primary action | Auth | Phase | Wireframe |
| --- | --- | --- | --- | --- | --- |
| `/` | Catalog: filter by track / difficulty / search, see budgets-at-a-glance and (7) solved state | Open a problem | anonymous | 1 | [catalog.md](wireframes/catalog.md) |
| `/problems/:slug` | Editor: statement + budgets, Monaco, results | Run (free problems anonymous) / Submit | anonymous for free | 1–2 | [editor.md](wireframes/editor.md) |
| `/sign-in/*`, `/sign-up/*` | Clerk components, themed with our tokens | Sign in | — | 1 | Clerk `appearance` from tokens |
| `/me` | Account: email, role, plan; (2) recent submissions; (7) progress | — | signed in | 1 → 7 | when 2.11 starts |
| `/me/progress` | Solved, attempts, streak, XP | Continue a path | signed in | 7 | in 7.1.2 |
| `/paths`, `/paths/:id` | Learning paths with prerequisites | Start next problem | signed in | 7 | in 7.2 |
| `/admin` … | Dashboard, problems table, problem editor, users, submissions, audit log | per page | `admin:access` | 3A | in 3A.8.1 |

## Shared chrome

- **App header (48 px):** wordmark · Problems · (7) Paths · spacer · theme toggle · Admin (only with
  `admin:access`) · Clerk `<UserButton>` or "Sign in".
- **Site header:** wordmark · Problems · How it works · Pricing · Blog · spacer · "Sign in" (link) ·
  "Start free" (primary button).
- **Footer (site only):** legal links, RSS, GitHub.
