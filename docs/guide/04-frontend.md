# Frontend: Astro marketing site and React app

Two static builds, each served by a small nginx container behind Traefik: the Astro site at `codebench.localhost` needs no auth, and the React app at `app.codebench.localhost` owns sign-in through Clerk and talks to the API with the Clerk session token.

## Marketing site (apps/marketing)

```bash
cd apps
npm create astro@latest marketing -- --template minimal --typescript strict
cd marketing
npx astro add tailwind sitemap mdx     # Astro ≥ 5.2: installs Tailwind v4 via @tailwindcss/vite
npm i -D lucide-static yaml            # build-time SVG icons; YAML parser for scripts/gen-problems.mjs
```

Keep `output: 'static'` in `astro.config.mjs`. The site never needs to know who is signed in: "Log in" and "Start free" are plain links into the app. (Clerk's Astro integration needs server rendering, which you can add later if you want an "Open app" button for signed-in visitors.)

```js
// astro.config.mjs — Tailwind v4 is a Vite plugin. Not @astrojs/tailwind: that integration is Tailwind v3 only
// and can't read tokens.css (@theme, @custom-variant).
import { defineConfig } from 'astro/config';
import tailwindcss from '@tailwindcss/vite';
import sitemap from '@astrojs/sitemap';
import mdx from '@astrojs/mdx';

export default defineConfig({
  site: process.env.SITE_URL ?? 'http://codebench.localhost',
  output: 'static',
  integrations: [sitemap(), mdx()],
  vite: { plugins: [tailwindcss()] },
});
```

```css
/* src/styles/global.css — imported once by src/layouts/BaseLayout.astro */
@import "tailwindcss";
@import "../../../../docs/design/tokens.css";   /* the single source; never copied (same import in apps/web) */
```

**Same design as the app, without React.** The site uses the same `tokens.css` (including the shadcn bridge, so
`bg-primary`, `text-muted-foreground`, `border-input` mean the same thing in both apps) and ships **no client
framework** — no React islands, so shadcn/ui, zustand and lucide-react stay in `apps/web` (ADR 0007). The few
shared components (Button, Badge, DifficultyBadge) are `.astro` files in `src/components/` whose classes mirror
the `cva` variants of the app's components; `docs/design/components.md` is the reference for both. Icons are the
same Lucide set, inlined at build time from `lucide-static` (e.g. `import check from 'lucide-static/icons/circle-check.svg?raw'`
+ `<Fragment set:html={check} />`, `aria-hidden="true"`). Dark mode follows `prefers-color-scheme` through a
two-line inline script in `BaseLayout` that sets `<html class="dark">` before first paint; the site has no theme
toggle, and it can't share the app's stored choice (different origin, different `localStorage`).

Pages to build:

| Route | Source | Content |
| --- | --- | --- |
| `/` | `src/pages/index.astro` | Hero, three "try it" cards that deep-link into free problems, track overview, pricing teaser |
| `/problems` | content collection `problems` | Catalog by track and difficulty (summaries only, never tests) |
| `/problems/[slug]` | `getStaticPaths` over the collection | SEO page per problem with a "Solve in the editor" link |
| `/how-it-works` | MDX | Grading explained: budgets, query plans, sandbox |
| `/pricing` | Astro | Free / Pro tiers (Paddle sandbox later) |
| `/blog/[slug]` | content collection `blog` | Articles; RSS via `@astrojs/rss` |
| `/legal/*` | MDX | Terms, privacy, acceptable use |

Generate `src/content/problems/*.md` from the same `problems/*/problem.yaml` files the API seeds from (`scripts/gen-problems.mjs`; it reads `../../problems` and writes **summaries only** — title, track, difficulty, free, tags, limits, the statement's first paragraph, test counts — FE-13). It works from an **allow-list** of fields, so anything added to `problem.yaml` later stays private by default, and it publishes only problems whose grader is live (`LIVE_GRADERS`, mirroring `ProblemSeeder.ImplementedGraders`), so no "Solve in the editor" link points at a problem the app can't open. The generated folder is git-ignored. The catalog matches the seed files; problems added later in the admin console appear on the site only once exported to YAML (ADR 0005: the DB is the source of truth after seeding).

The npm scripts chain the steps explicitly — `"build": "node scripts/gen-problems.mjs && astro build && node scripts/verify-dist.mjs"` (same for `dev` and `check`) — rather than relying on `pre*`/`post*` hooks, which npm 12 no longer runs. `scripts/verify-dist.mjs` is a leak guard: it fails the build if any reference-solution line, hidden-test value, seed SQL or source map from any `problem.yaml` appears in `dist/` (a local, always-on version of REA check RA-06).

Deep links into the app carry intent, e.g. `http://app.codebench.localhost/problems/sum-of-evens?entry=home-hero`, so you can measure which entry points convert.

`apps/marketing/Dockerfile` — the build context stays `apps/marketing` (the root `.dockerignore` excludes `apps/`
and `docs/`), and `docs/design` + `problems` arrive as **additional build contexts** mounted at the same relative
paths as on your machine, so `../../docs/design/tokens.css` and `../../problems` resolve identically:

```dockerfile
FROM node:24-alpine AS build
WORKDIR /repo/apps/marketing
COPY package*.json ./
RUN npm ci
COPY . .
COPY --from=design . /repo/docs/design
COPY --from=problems . /repo/problems     # build stage only: the final image gets dist/ (summaries) and nothing else
ARG SITE_URL=http://codebench.localhost
ARG APP_URL=http://app.codebench.localhost
ENV SITE_URL=$SITE_URL PUBLIC_APP_URL=$APP_URL
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /repo/apps/marketing/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
```

```yaml
# deploy/docker-compose.yml (marketing)
build:
  context: ../apps/marketing
  additional_contexts: { design: ../docs/design, problems: ../problems }
```

## Web app (apps/web)

```bash
cd apps
npm create vite@latest web -- --template react-ts
cd web
npm i @clerk/clerk-react react-router-dom @tanstack/react-query @monaco-editor/react zod zustand lucide-react
npm i -D tailwindcss @tailwindcss/vite @types/node vitest @testing-library/react msw
npx shadcn@latest init        # after adding the "@/*" path alias to tsconfig + vite.config.ts
npx shadcn@latest add button badge tabs tooltip dialog dropdown-menu input label select separator skeleton
```

**Stack roles** — each library has one job:

| Library | Job | Not for |
| --- | --- | --- |
| TypeScript (strict) | Types everywhere; `z.infer` for API shapes | `any` |
| Tailwind CSS v4 | All styling, via the colour roles in `docs/design/tokens.css` | Raw palette colours (`bg-zinc-900`) |
| shadcn/ui | Accessible primitives (Radix) copied into `src/components/ui/`, re-skinned to `docs/design/components.md` | Shipping shadcn's default look |
| lucide-react | Icons (verdicts, actions, theme toggle) — named imports only | `import * as Icons`, icon lookup by string |
| zod | Parse API responses that drive logic and every form | — |
| TanStack Query | **Server state**: everything that comes from the API | — |
| zustand | **Client UI state** only: theme, editor layout, per-problem drafts | API data, auth tokens, anything secret |

shadcn components are source code we own after `add`; review them like any other change. Its CSS variables
(`--primary`, `--muted-foreground`, `--ring`, …) are mapped onto our tokens by the *shadcn bridge* in
`tokens.css`, so the components inherit CodeBench's design and its measured contrast. Clerk's sign-in UI can
use Clerk's shadcn theme so it matches (see `context/clerk_prompt.md`, "If using shadcn/ui").

Folder structure:

```text
apps/web/src/
├─ main.tsx                 # ClerkProvider, QueryClientProvider, RouterProvider
├─ config.ts                # reads window.__CONFIG__ (runtime) with Vite env fallback
├─ index.css                # @import "tailwindcss"; "tw-animate-css"; "../../../docs/design/tokens.css" (incl. shadcn bridge)
├─ lib/api.ts               # useApi(): fetch wrapper adding the Clerk token
├─ lib/utils.ts             # cn() = clsx + tailwind-merge (shadcn)
├─ components/ui/           # shadcn/ui primitives (generated, kebab-case files) — re-skinned, not redesigned
├─ components/              # our shared components (Button wrappers, VerdictBadge, BudgetBar, …)
├─ stores/                  # zustand stores: useThemeStore, useEditorLayoutStore, useDraftStore
├─ features/
│  ├─ problems/            # ProblemList, ProblemFilters, useProblems
│  ├─ editor/              # EditorPage, CodeEditor (Monaco), StatementPane
│  ├─ submissions/         # RunButton, SubmitButton, ResultsPanel, useSubmission
│  └─ account/             # Progress, Settings
└─ routes.tsx
```

### Clerk setup

In the Clerk dashboard (development instance): enable email + GitHub sign-in, and copy the publishable key. Development instances accept `localhost` origins; add `http://app.codebench.localhost` if the dashboard asks for allowed origins or redirect URLs.

```tsx
// main.tsx
import { ClerkProvider } from '@clerk/clerk-react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router-dom';
import { config } from './config';
import { router } from './routes';

const queryClient = new QueryClient();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <ClerkProvider publishableKey={config.clerkPublishableKey} afterSignOutUrl={config.marketingUrl}>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </ClerkProvider>,
);
```

Routes: `/` (catalog), `/problems/:slug` (editor, public for free problems), `/sign-in/*` and `/sign-up/*` (Clerk's `<SignIn routing="path" path="/sign-in" />`), `/me` (progress, wrapped in `<SignedIn>` / `<RedirectToSignIn />`).

### API client

```ts
// lib/api.ts
import { useAuth } from '@clerk/clerk-react';
import { config } from '../config';

export function useApi() {
  const { getToken, isSignedIn } = useAuth();

  return async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers);
    headers.set('Content-Type', 'application/json');
    if (isSignedIn) headers.set('Authorization', `Bearer ${await getToken()}`);
    const res = await fetch(`${config.apiUrl}${path}`, { ...init, headers });
    if (!res.ok) throw await ApiError.from(res);   // parse RFC ProblemDetails from the API
    return res.status === 204 ? (undefined as T) : res.json();
  };
}
```

`getToken()` returns a short-lived session token and refreshes it for you; call it per request rather than caching it.

### Run, Submit and polling

```ts
// features/submissions/useSubmission.ts
export function useSubmitCode(kind: 'runs' | 'submissions') {
  const api = useApi();
  return useMutation({
    mutationFn: (body: { problemId: string; code: string }) =>
      api<{ id: string }>(`/${kind}`, {
        method: 'POST',
        body: JSON.stringify(body),
        headers: { 'Idempotency-Key': crypto.randomUUID() },   // matches the template's .WithIdempotency()
      }),
  });
}

export function useSubmissionResult(id?: string) {
  const api = useApi();
  return useQuery({
    queryKey: ['submission', id],
    enabled: !!id,
    queryFn: () => api<SubmissionResult>(`/submissions/${id}`),
    refetchInterval: (q) => (q.state.data?.status === 'Graded' || q.state.data?.status === 'Failed' ? false : 750),
  });
}
```

The submission id, status and results stay in TanStack Query — don't copy them into a zustand store; derive UI from the query.

Switch to Server-Sent Events later (`GET /submissions/{id}/events`) once polling works; keep polling as the fallback.

### Editor page layout

- **Left pane:** problem statement (Markdown), constraints, budgets (time, allocations, plan rules) shown up front.
- **Right pane:** Monaco editor (`language="csharp"`, starter code; save drafts to `localStorage` per problem via a zustand `persist` store keyed by slug), with Run and Submit buttons.
- **Bottom panel:** per-test rows: name, pass/fail, elapsed (µs), allocated bytes, budget bar; expandable details for SQL and plan summaries on database problems; compiler errors mapped to Monaco markers by line and column.
- **Signed out:** Run works on free problems; Submit opens Clerk sign-in and resubmits after return (store the pending code in `sessionStorage`).

### Runtime configuration in Docker

Vite bakes `import.meta.env` at build time, which means one image per environment. Instead, generate `/config.js` when the container starts, so one image runs anywhere:

```html
<!-- index.html, before the app bundle -->
<script src="/config.js"></script>
```

```ts
// config.ts
declare global { interface Window { __CONFIG__?: Record<string, string> } }
const c = window.__CONFIG__ ?? {};
export const config = {
  apiUrl: c.API_URL ?? import.meta.env.VITE_API_URL,
  clerkPublishableKey: c.CLERK_PUBLISHABLE_KEY ?? import.meta.env.VITE_CLERK_PUBLISHABLE_KEY,
  marketingUrl: c.MARKETING_URL ?? import.meta.env.VITE_MARKETING_URL,
};
```

`apps/web/Dockerfile`:

```dockerfile
FROM node:24-alpine AS build
WORKDIR /repo/apps/web
COPY package*.json ./
RUN npm ci
COPY . .
COPY --from=design . /repo/docs/design   # additional build context (compose: additional_contexts: { design: ../docs/design })
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /repo/apps/web/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
# nginx's official image runs /docker-entrypoint.d/*.sh at start
COPY docker/40-config.sh /docker-entrypoint.d/40-config.sh
RUN chmod +x /docker-entrypoint.d/40-config.sh
```

```sh
#!/bin/sh
# docker/40-config.sh — write runtime config from env vars
cat > /usr/share/nginx/html/config.js <<EOF
window.__CONFIG__ = {
  API_URL: "${API_URL}",
  CLERK_PUBLISHABLE_KEY: "${CLERK_PUBLISHABLE_KEY}",
  MARKETING_URL: "${MARKETING_URL}"
};
EOF
```

`nginx.conf` (SPA fallback, long cache for hashed assets, no cache for `config.js` and `index.html`):

```nginx
server {
  listen 80;
  root /usr/share/nginx/html;
  location /assets/ { add_header Cache-Control "public, max-age=31536000, immutable"; }
  location = /config.js { add_header Cache-Control "no-store"; }
  location / { try_files $uri /index.html; add_header Cache-Control "no-cache"; }
}
```

Only the publishable key goes in `config.js`. The Clerk secret key and webhook secret belong to the API alone.

### Client UI state (zustand)

```ts
// stores/useDraftStore.ts — per-problem drafts survive reloads; nothing secret, no API data
import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

type DraftState = {
  drafts: Record<string, string>;            // slug → code
  setDraft: (slug: string, code: string) => void;
  clearDraft: (slug: string) => void;
};

export const useDraftStore = create<DraftState>()(
  persist(
    (set) => ({
      drafts: {},
      setDraft: (slug, code) => set((s) => ({ drafts: { ...s.drafts, [slug]: code } })),
      clearDraft: (slug) =>
        set((s) => ({ drafts: Object.fromEntries(Object.entries(s.drafts).filter(([key]) => key !== slug)) })),
    }),
    { name: 'codebench-drafts', version: 1, storage: createJSONStorage(() => localStorage) },
  ),
);
```

Components select the slice they need (`useDraftStore((s) => s.drafts[slug])`) so unrelated updates don't
re-render them. If you add zustand's `devtools` middleware, wrap it so it only exists in development builds —
the REA bundle audit (RA-05) fails on `__REDUX_DEVTOOLS_EXTENSION__` in `dist/`.

### Marketing site and the new stack

shadcn/ui and zustand are React-only and stay in `apps/web`; lucide-react has its build-time twin
`lucide-static` on the site. See "Same design as the app, without React" in the marketing section above:
same `tokens.css` (one file, imported by relative path in both apps), `.astro` components mirroring the app's
variant classes, no client framework, dark mode from `prefers-color-scheme`.

## Frontend tests

- Vitest + Testing Library for components; MSW to mock the API (including a submission that goes Pending → Graded).
- Mock Clerk with a thin wrapper around `useAuth` so tests can toggle signed-in state.
- One Playwright smoke test in CI against the full Compose stack: open a free problem, press Run, see a result row (see the CI/CD tab).
