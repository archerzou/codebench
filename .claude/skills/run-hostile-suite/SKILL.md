---
name: run-hostile-suite
description: Build a CodeBench sandbox image and run the hostile-submission suite (tests/hostile-submissions) against it with the worker's exact container flags, then report each case's verdict against expected.json. Use when the user changes Sandbox.Runner, a sandbox Dockerfile, the worker's container settings, or asks to test sandbox security.
argument-hint: [grader kind, default "algorithms"]
---

# Run the Hostile Suite

Guide 03 (threat model, hardening checklist) and guide 06 (`sandbox-images.yml`); rules SB-01..SB-06.
Every case is code **you wrote** to attack **your own** sandbox — never point this at anything else.

## Workflow

1. **Kind** — `$ARGUMENTS` or `algorithms`. Image tag: `codebench-sandbox-<kind>:test`.
2. **Build**
   ```bash
   docker build -f sandbox-images/<kind>.Dockerfile -t codebench-sandbox-<kind>:test .
   ```
   If the Dockerfile doesn't exist yet, stop: the grader isn't built (Phase 2 for `algorithms`).
3. **Runner script** — `scripts/test-sandbox.sh <image> tests/hostile-submissions`. If it doesn't exist,
   create it. It must run each case with **exactly** the worker's flags (read them from
   `src/Worker/SandboxRunner.cs` — never weaker):
   ```bash
   docker run --rm -i --network none --read-only \
     --tmpfs /tmp:rw,size=64m,noexec,nosuid \
     --cap-drop ALL --security-opt no-new-privileges --user 10001:10001 \
     --pids-limit 128 --memory 256m --memory-swap 256m --cpus 1 \
     <image> --problem <slug> --hidden true < <bundle+code input>
   ```
   with a wall-clock `timeout` matching the case's limit, stdout capped at 1 MB, and the verdict taken
   only from the nonce-delimited block. Compare with `<case>/expected.json`; exit non-zero on any mismatch.
4. **Cases** (each `tests/hostile-submissions/<case>/` has `Solution.cs` + `expected.json`) — at minimum:
   outbound `HttpClient` (never Accepted), `while(true)` (Timeout), 2 GB allocation (killed →
   BudgetExceeded/InternalError), process spawning (pids limit), writes to `/etc` and `/opt` (denied),
   fake success JSON (ignored), a correct solution (Accepted). Add grader-specific cases for other kinds.
5. **Run** (Git Bash / WSL / CI):
   ```bash
   ./scripts/test-sandbox.sh codebench-sandbox-<kind>:test tests/hostile-submissions
   ```
6. **Afterwards** confirm nothing is left running: `docker ps -a --filter ancestor=codebench-sandbox-<kind>:test`.

## Report

A table: case · expected · actual · pass/fail · notes (e.g. exit code, time). Any failure is a 🔴 SB
blocker — explain which control failed (network, limit, timeout, parsing) and the fix. Don't loosen a
flag or an expectation to make a case pass.
