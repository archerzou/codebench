---
name: add-react-feature
description: Add a feature folder to the CodeBench React app (apps/web/src/features/<name>) — zod types, TanStack Query hooks over the useApi client, components, routes, and a Vitest + Testing Library test with MSW mocks. Use when the user wants a new page, panel, or client-side feature in the web app.
argument-hint: <feature, e.g. "bookmarks: list and toggle bookmarked problems">
---

# Add a React Feature

Follow the FE-* rules in `context/code_rules.md` and guide 04. The API endpoint should exist first
(`/add-feature`); check its shape in `docs/api-contract.md` or Scalar.

## Layout

```
apps/web/src/features/<name>/
  types.ts            zod schemas + z.infer types for request/response shapes
  use<Name>.ts        TanStack Query hooks (useQuery / useMutation) via useApi()
  <Component>.tsx     one component per file, named exports
  index.ts            the feature's public surface (what other features may import)
  <Component>.test.tsx
```

## Workflow

1. **Types** — zod schemas mirroring the API `Response`; parse responses that drive logic *[FE-06]*.
2. **Hooks** — only `useApi()` from `lib/api.ts` performs HTTP *[FE-04]*; never `fetch` in components or
   hooks directly. Query keys start with the resource: `['bookmarks']`, `['bookmarks', id]`. Mutations
   invalidate affected keys; work-creating POSTs send `Idempotency-Key: crypto.randomUUID()` *[FE-07]*.
   No `useEffect` data fetching *[FE-03]*.
3. **Components** — function components, typed `Props`, Tailwind utilities (colour **roles** only, variants
   with `cva` + `cn()`), semantic HTML, labels, keyboard access, `aria-live` for async results
   *[FE-09–FE-11]*. Errors render `ApiError.detail`. Build on our wrappers in `components/` or on shadcn
   primitives in `components/ui/`; need a new primitive? `npx shadcn@latest add <name>` (from `apps/web`),
   review the generated file and re-skin it — hover `bg-accent` → `bg-surface-sunken`, no `text-white`
   *[FE-16]*. Icons: `lucide-react` named imports, `aria-hidden` beside a visible word *[FE-17]*.
   Client-only UI state (layout, preferences, drafts) goes in a zustand store in `src/stores/` — never API
   data or tokens *[FE-15]*.
4. **Routing** — add the route in `routes.tsx`; signed-in-only pages wrap in `<SignedIn>` /
   `<RedirectToSignIn/>`; permission-gated UI uses `RequirePermission` (UX only — the API enforces) *[FE-08]*.
   Heavy or admin-only pages are `lazy()`-loaded.
5. **Config** — never read `import.meta.env` outside `config.ts`; never add a secret to runtime config *[FE-05]*.
6. **Test** — Vitest + Testing Library (query by role/label) with MSW handlers for the endpoints; cover
   loading, success, empty and error states; toggle auth via the `useAuth` test wrapper.
7. **Verify**
   ```powershell
   npm --prefix apps/web run lint
   npx --prefix apps/web tsc --noEmit -p apps/web
   npm --prefix apps/web test -- --run
   powershell -File scripts/check-rules.ps1
   ```

## Done when

Lint (zero warnings), type-check and tests pass; the feature works against the running API at
http://localhost:5173; no new `any`, `fetch` or `import.meta.env` outside the allowed files; no raw palette
classes, no `import * as` from `lucide-react`, no API data or tokens in `stores/`.
