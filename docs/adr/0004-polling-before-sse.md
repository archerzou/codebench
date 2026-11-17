# 4. Polling before Server-Sent Events

Status: accepted · 2026-11-17

## Context
Gradings take one to a few seconds. The browser needs to see status changes.

## Decision
The web app polls `GET /submissions/{id}` every 750 ms until `Graded`/`Failed` (TanStack Query
`refetchInterval`). SSE (`GET /submissions/{id}/events`) may be added later, with polling kept as fallback.

## Consequences
Simple and cache-friendly; slightly more requests. No sticky connections through Traefik.
