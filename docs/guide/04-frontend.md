# Frontend: Astro marketing site and React app

Two static builds, each served by a small nginx container behind Traefik: the Astro site at `codebench.localhost` needs no auth, and the React app at `app.codebench.localhost` owns sign-in through Clerk and talks to the API with the Clerk session token.

## Marketing site (apps/marketing)

```bash
cd apps
npm create astro@latest marketing -- --template minimal --typescript strict
cd marketing
npx astro add tailwind sitemap mdx
```

Keep `output: 'static'` in `astro.config.mjs`. The site never needs to know who is signed in: "Log in" and "Start free" are plain links into the app. (Clerk's Astro integration needs server rendering, which you can add later if you want an "Open app" button for signed-in visitors.)

```js
// astro.config.mjs
import { defineConfig } from 'astro/config';
import tailwind from '@astrojs/tailwind';
import sitemap from '@astrojs/sitemap';
import mdx from '@astrojs/mdx';

export default defineConfig({
  site: process.env.SITE_URL ?? 'http://codebench.localhost',
  output: 'static',
  integrations: [tailwind(), sitemap(), mdx()],
});
```

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

Generate `src/content/problems/*.md` from the same `problems/*/problem.yaml` files the API seeds from (a tiny Node script run before `astro build`), so the catalog never drifts from the database.

Deep links into the app carry intent, e.g. `http://app.codebench.localhost/problems/two-sum?entry=home-hero`, so you can measure which entry points convert.

`apps/marketing/Dockerfile`:

```dockerfile
FROM node:24-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
ARG SITE_URL=http://codebench.localhost
ARG APP_URL=http://app.codebench.localhost
ENV SITE_URL=$SITE_URL PUBLIC_APP_URL=$APP_URL
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
```

## Web app (apps/web)

```bash
cd apps
npm create vite@latest web -- --template react-ts
cd web
npm i @clerk/clerk-react react-router-dom @tanstack/react-query @monaco-editor/react zod
npm i -D tailwindcss @tailwindcss/vite vitest @testing-library/react msw
```

Folder structure:

```text
apps/web/src/
├─ main.tsx                 # ClerkProvider, QueryClientProvider, RouterProvider
├─ config.ts                # reads window.__CONFIG__ (runtime) with Vite env fallback
├─ lib/api.ts               # useApi(): fetch wrapper adding the Clerk token
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

Switch to Server-Sent Events later (`GET /submissions/{id}/events`) once polling works; keep polling as the fallback.

### Editor page layout

- **Left pane:** problem statement (Markdown), constraints, budgets (time, allocations, plan rules) shown up front.
- **Right pane:** Monaco editor (`language="csharp"`, starter code; save drafts to `localStorage` per problem), with Run and Submit buttons.
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
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
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

## Frontend tests

- Vitest + Testing Library for components; MSW to mock the API (including a submission that goes Pending → Graded).
- Mock Clerk with a thin wrapper around `useAuth` so tests can toggle signed-in state.
- One Playwright smoke test in CI against the full Compose stack: open a free problem, press Run, see a result row (see the CI/CD tab).
