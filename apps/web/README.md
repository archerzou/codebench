# apps/web — React + Vite + TS app (`app.codebench.localhost`)

Phase 1. See [docs/guide/04-frontend.md](../../docs/guide/04-frontend.md) and the admin console in
[docs/guide/09-seed-data-and-admin.md](../../docs/guide/09-seed-data-and-admin.md).

Scaffold (run from `apps/`; Vite refuses a non-empty folder, so move this README aside first):

```bash
npm create vite@latest web -- --template react-ts
cd web
npm i @clerk/clerk-react react-router-dom @tanstack/react-query @monaco-editor/react zod zustand lucide-react
npm i -D tailwindcss @tailwindcss/vite @types/node vitest @testing-library/react msw
npx shadcn@latest init    # after the "@/*" alias; then delete init's palette and import docs/design/tokens.css
```

Target layout: `src/{main.tsx,config.ts,routes.tsx,index.css,lib/{api,utils}.ts,components/ui,components,stores,features/{problems,editor,submissions,account,admin}}`,
plus `Dockerfile` (additional build context `design`), `nginx.conf` and `docker/40-config.sh` (runtime
`config.js`; publishable keys only). Stack roles and rules: ADR 0007, FE-15/16/17.
