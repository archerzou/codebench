# Hostile submissions

Phase 2. Submissions you write yourself that try to escape or abuse the sandbox. `scripts/test-sandbox.sh`
runs each one with the worker's exact `docker run` flags and compares the verdict with `expected.json`.
See [docs/guide/06-ci-cd.md](../../docs/guide/06-ci-cd.md) and the hardening checklist in
[docs/guide/03-worker-and-sandbox.md](../../docs/guide/03-worker-and-sandbox.md).

| Case | Expected verdict |
| --- | --- |
| `HttpClient` to an external host | network error; never Accepted |
| `while (true) { }` | Timeout |
| allocate 2 GB | killed: BudgetExceeded or InternalError |
| spawn processes in a loop | pids limit reached |
| write to `/etc` or `/opt` | permission denied |
| print a fake success JSON | ignored; real result read from the nonce block |
| correct O(n) solution | Accepted |
