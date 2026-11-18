# 6. No recon of Katabench; design from our own requirements

Status: accepted · 2026-11-18 (supersedes the recon workflow in guide 01 "Recon workflow (Phase 0)"
and the `_recon_` column of `docs/feature-map.md`)

## Context
Phase 0 planned an observation-only recon of Katabench (marketing site, app shell, Run flow, auth
hand-off) with REA, to inform CodeBench's feature map and API contract. Its Terms (last updated
2026-11-11) forbid using the Service "to build, train, or benchmark a competing product", forbid
reverse-engineering, and the AUP forbids "scripted scraping of the API" and probing the sandbox.

## Decision
- Do not observe, capture, or analyse Katabench's site, app, or API, manually or with REA.
- Features and the API are specified from `docs/guide/` and our own judgement; `feature-map.md`
  drops the "observed" column; `api-contract.md` is the authority.
- REA stays in the toolbox only for our own artefacts (web bundle, Worker/API assemblies, sandbox).

## Consequences
- Phase 0 items 0.4.3–0.4.4 close as "not applicable"; 0.4.2 (REA) moves to Phase 4.4.
- No third-party UX patterns, routes, or response shapes inform the design; anything resembling
  another product comes from common practice, not observation.
- Revisit only with written permission from Katabench.

## Addendum 2026-11-19 — screenshots and design
- Full-page screenshots of katabench.com / app.katabench.com placed in `docs/designs/` are **not** a
  design reference: building our UI from them is the same "use its content to build a competing product"
  problem, and would reproduce their visual identity. The folder is git-ignored (public repo) and should be
  deleted locally.
- CodeBench gets an original design track instead (build plan §1.0, `docs/design/`): brief, tokens, page
  inventory and wireframes derived from `feature-map.md`.
- REA is wired in as a self-audit track over our own artefacts (build plan Appendix D, RA-01..RA-09).
