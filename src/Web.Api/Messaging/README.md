# Messaging (NATS JetStream)

Phase 2. Infrastructure for the grading pipeline; not a feature, so it lives outside `Features/`.
See [docs/guide/02-backend-api.md](../../../docs/guide/02-backend-api.md) §6–7 and doc 09 (problem bundles).

- `OutboxMessage` entity + configuration (written in the same transaction as the `Submission`).
- `OutboxPublisher` — `BackgroundService`, `FOR UPDATE SKIP LOCKED`, publishes with `MsgId` for dedupe.
- `GradeResultConsumer` — durable `api-results` on stream `RESULTS`; idempotent (already Graded → ack).
- `JetStreamSetup` — creates streams `JOBS` / `RESULTS` and the `problem-bundles` object store at startup.
- `ProblemBundlePublisher` — writes `{slug}/v{version}` on publish.

Message records live in `src/Contracts` (`CodeBench.Contracts`), never here.
