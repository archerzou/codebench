---
name: add-admin-crud
description: Add an admin capability to CodeBench end to end — admin slices under Features/Admin mapped in the /admin group with the right permission, audit logging, cache invalidation, integration tests for 403/409 paths, and the matching /admin page in the React app (table, form, permission guard). Use when the user wants to manage something from the admin console.
argument-hint: <what to manage, e.g. "learning paths: list, create, edit, reorder">
---

# Add Admin CRUD

Read guide 09 ("Roles and the admin account", "Admin API", "Admin UI") and rules EP-04, DM-07, QY-05,
SC-03, TS-03, FE-08 in `context/code_rules.md`.

## Backend

1. **Entity** — if new, use `/add-entity`. Human-edited entities get an `xmin` concurrency token *[DM-07]*.
2. **Slices** — one file each in `src/Web.Api/Features/Admin/` via `/add-feature`: `List{X}` (PagedList,
   filters), `Get{X}ForEdit`, `Create{X}`, `Update{X}` (`If-Match` → 409 `ErrorType.Conflict` on stale
   version), `Delete{X}` or `Archive{X}`.
3. **Routing & auth** — map inside the `/admin` route group (`admin:access`, `Tags.Admin`) and add the
   specific permission per endpoint with `.HasPermission(Permissions.X)`. New permission → add the constant
   and the role matrix row in `RoleSeeder` (admin always; author only if authors should have it).
4. **Audit** — make sure the entity is covered by the audit `SaveChangesInterceptor` (before/after JSON,
   acting user).
5. **Caches** — every mutating slice invalidates the learner-facing caches it affects *[QY-05]*.
6. **Never leak** — admin `Response` types may include hidden data; make sure no learner route reuses them
   *[QY-04]*.
7. **Tests** — integration: learner → 403, author → allowed/forbidden per the matrix, admin → OK; stale
   `If-Match` → 409; audit row written. Unit: handler failure paths + validator rules.

## Frontend

8. **Page** in `apps/web/src/features/admin/` (lazy-loaded under `/admin`, wrapped by `RequirePermission`):
   - Table: `@tanstack/react-table` with server-side paging/filtering matching the List slice.
   - Form: `react-hook-form` + zod resolver; send `If-Match` with the loaded version; show 409 as
     "changed by someone else — reload".
   - Add the nav entry in `AdminLayout`; hide actions the user lacks permission for (UX only).
   - Follow the `add-react-feature` skill for hooks, MSW test and verification.

## Done when

`dotnet test` (incl. the 403/409 cases) and frontend tests pass; `scripts/check-rules.ps1` passes; the page
works for an admin and is unreachable (API 403) for a learner. Tick the item in `context/progress_tracker.md`.
