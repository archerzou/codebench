# sandbox-images

One image per grader kind, built in CI (`sandbox-images.yml`) and pulled by the sandbox daemon.
See [docs/guide/03-worker-and-sandbox.md](../docs/guide/03-worker-and-sandbox.md).

| File | Phase | Contents |
| --- | --- | --- |
| `algorithms.Dockerfile` | 2 | .NET runtime + published `src/Sandbox.Runner`, non-root uid 10001 |
| `database.Dockerfile` | 5 | algorithms image + PostgreSQL started inside the sandbox (data dir in `/tmp`) |
| `db-entrypoint.sh` | 5 | `initdb` into tmpfs, start postgres on a Unix socket, exec the runner |

Tests are **not** baked in: per [doc 09](../docs/guide/09-seed-data-and-admin.md), the worker pipes the
versioned problem bundle (from the NATS object store) into the sandbox together with the user code.
