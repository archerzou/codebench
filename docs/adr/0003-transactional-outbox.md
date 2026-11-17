# 3. Transactional outbox over direct publish

Status: accepted · 2026-11-17

## Context
Creating a submission must both persist it and enqueue grading. Publishing to NATS inside the
request risks a saved submission with no job (or a job for a rolled-back submission).

## Decision
Write the `Submission` and an `OutboxMessage` in one `SaveChangesAsync`. A background `OutboxPublisher`
claims rows with `FOR UPDATE SKIP LOCKED` and publishes with `MsgId = outbox id`, so JetStream
de-duplicates retries. The results consumer is idempotent (already `Graded` → just ack).

## Consequences
- Up to ~500 ms extra latency from the polling interval.
- Partial index on `outbox_messages (processed_at) WHERE processed_at IS NULL`.
