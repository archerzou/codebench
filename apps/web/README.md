# apps/web — React + Vite + TS app (`app.codebench.localhost`)

Phase 1. See [docs/guide/04-frontend.md](../../docs/guide/04-frontend.md) and the admin console in
[docs/guide/09-seed-data-and-admin.md](../../docs/guide/09-seed-data-and-admin.md).

Scaffold (run from `apps/`; Vite refuses a non-empty folder, so move this README aside first):

```bash
npm create vite@latest web -- --template react-ts
cd web
npm i @clerk/clerk-react react-router-dom @tanstack/react-query @monaco-editor/react zod
npm i -D tailwindcss @tailwindcss/vite vitest @testing-library/react msw
```

Target layout: `src/{main.tsx,config.ts,routes.tsx,lib/api.ts,features/{problems,editor,submissions,account,admin}}`,
plus `Dockerfile`, `nginx.conf` and `docker/40-config.sh` (runtime `config.js`; publishable keys only).
