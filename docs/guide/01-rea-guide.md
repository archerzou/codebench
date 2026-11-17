# REA guide: recon and self-inspection

REA gives you two things on this project: a short, observation-only recon of Katabench's public client side (one or two evenings), and a tool you keep using on your *own* builds to check bundles, assemblies and API behaviour. It cannot see Katabench's backend, and you should not try.

## What REA is

[REA](https://github.com/morluto/rea) ("Reverse Engineer Anything") is an MCP server plus CLI. Your coding agent (Claude Code here) calls its tools to inspect targets without source code and gets back findings with evidence and stated unknowns. Analysis runs locally.

Targets relevant to CodeBench:

| Target | What REA returns | Needs | Use on this project |
| --- | --- | --- | --- |
| Websites | Page structure, scripts, network observations, screenshots | A Chrome-family browser | Recon of katabench.com and the app (observe only) |
| Saved network captures (HAR) | Requests, responses, payloads, source locations | A `.har` file | Your own browser session, exported from DevTools |
| JavaScript apps | Modules, imports, source maps, routes | Node.js + npm | Audit **your** React/Astro `dist/` builds |
| .NET assemblies | Metadata, CIL, native dependencies, build comparisons | Nothing extra (static) | Audit **your** published API, Worker and grader DLLs |
| Process behaviour | Terminal output, filesystem observations, run comparisons | Linux/macOS with a PTY | Observe **your** sandbox harness while hardening it |

Native-binary engines (Hopper, Ghidra, IDA, Kuna) are **not** needed for this project.

## Install and update

Requirements: Node.js 22.x (≥ 22.23.4), 24.x (≥ 24.22.1) or 26.12.1+, plus npm. On Linux, a procps-compatible `ps` must be on `PATH` for static JavaScript analysis.

```bash
# 1. Register REA with your agent (choose Claude Code), review the diff, approve
npx rea-agents setup
# restart Claude Code afterwards

# 2. Optional: install the CLI for direct use
npm install --global rea-agents
rea --help

# 3. Keep it current (REA ships fixes frequently)
rea update                     # global CLI install
npx rea-agents@latest setup    # refresh agent registration if you use npx
```

Run package acquisition from a trusted directory outside your project. Only install from the official sources: the `morluto/rea` repo, rea.tools, or the `rea-agents` npm package.

**Context tips:** REA uses compact input schemas by default. If your client caps the tool list, set `REA_MCP_OUTPUT_SCHEMA_PROFILE=compact` in the MCP registration; set `REA_MCP_INPUT_SCHEMA_PROFILE=full` only when you need every field's guidance.

## Rules of engagement for Katabench recon

| Do | Don't |
| --- | --- |
| Browse pages and try the free puzzles as a normal user | Send crafted code or payloads designed to probe their sandbox |
| Observe network calls your own clicks trigger | Replay, modify or automate their API requests |
| Note UX patterns, route structure, auth hand-off | Download or store puzzle statements, hidden tests or reference solutions |
| Write findings in your own words | Copy marketing copy, logos, illustrations or CSS |
| Read their Terms and Acceptable Use first | Create multiple accounts or bypass the free-tier cap |

## Recon workflow (Phase 0)

1. **Create the docs folder** in your repo: `docs/recon.md`, `docs/feature-map.md`, `docs/api-contract.md`.
2. **Map the marketing site.** Prompt Claude Code:

```text
Use REA browser observation on https://katabench.com (normal browsing only).
- List top-level routes and the navigation structure.
- Describe how "Log in" and "Try free" hand off to app.katabench.com
  (link targets, query parameters such as path/p/entry).
- Note which pages look static vs. dynamic, and any analytics/consent behaviour.
Write findings to docs/recon.md under "Marketing site", with evidence and unknowns.
Do not download page assets or copy text.
```

3. **Map the app shell** (signed out, free puzzle):

```text
Use REA browser observation on https://app.katabench.com/?p=two-sum.
I will click Run once with the starter code unchanged.
- List client routes and the layout regions of the editor page.
- For the Run click: endpoint paths, HTTP methods, status codes, and whether the
  result arrives by polling, SSE or WebSocket. Describe response *shapes* only
  (field names and types), not content.
Do not replay, modify or repeat any request.
Append to docs/recon.md under "App".
```

4. **Map the auth hand-off** (optional, your own account):

```text
Observe sign-in at https://app.katabench.com/login using REA. Record which
Clerk components/flows appear and where the browser is redirected afterwards.
Do not capture or store any token values. Append to docs/recon.md under "Auth".
```

5. **Alternative to live observation:** record a HAR in Chrome DevTools (Network tab → *Export HAR*) while you click through as a normal user, then ask the agent: *"Use REA to analyze ./captures/katabench-session.har. Summarise endpoints and response shapes only; redact cookies and Authorization headers."* Delete the HAR afterwards; it contains your session cookies.
6. **Translate, don't transcribe.** Turn `recon.md` into your own `feature-map.md` (features you will build, prioritised) and `api-contract.md` (your own endpoint design). Template below.

## Templates for the recon outputs

```markdown
# feature-map.md
| Feature | Observed on Katabench (own words) | CodeBench version | Phase |
|---|---|---|---|
| Try before signup | Free puzzle runs without login | Anonymous Run on 3 free problems, rate-limited | 1 |
| Per-test results | Time + allocations per test | Same, measured in sandbox harness | 2 |
| Query-plan grading | Plan rules shown pass/fail | EXPLAIN (FORMAT JSON) rules | 4 |

# api-contract.md (yours)
GET  /problems?track=&difficulty=&page=      -> PagedList<ProblemSummary>
GET  /problems/{slug}                          -> ProblemDetail (statement, starter, visible tests)
POST /runs            {problemId, code}        -> 202 {id}
POST /submissions     {problemId, code}        -> 202 {id}
GET  /submissions/{id}                         -> SubmissionResult
GET  /me/progress                              -> Progress
POST /webhooks/clerk                           -> 204 (Svix-verified)
```

## Using REA on your own builds (ongoing)

These are the uses that stay valuable for the life of the project, because you own every target.

**Audit the frontend bundles before each release:**

```bash
cd apps/web && npm run build
npx -y rea-agents@latest analyze-javascript-application "$(pwd)/dist" --json > ../../artifacts/web-bundle.json
```

Then ask the agent: *"From artifacts/web-bundle.json, list any hard-coded URLs, keys or tokens, any source maps shipped to production, and any route the router exposes that the API doesn't protect."* Clerk's publishable key is meant to be public; a Clerk **secret** key, Paddle API key or internal hostname is not.

**Audit the .NET artifacts:**

```text
Use REA managed-code analysis on src/Worker/bin/Release/net10.0/publish/Worker.dll.
Confirm it declares no dependency on Npgsql or EF Core, and list every
configuration key it reads. Compare with the previous build in ./artifacts/prev/.
```

This turns "the worker has no DB access" from a convention into something you verify on every build.

**Inspect your sandbox harness behaviour** (Linux/macOS, process capture): ask REA to run the grader harness against a deliberately hostile test submission *you wrote* (tries to open a socket, write outside `/tmp`, fork-bomb) and report the filesystem and exit observations. Use the results to tune limits in the *Worker & Sandbox* tab.

**Build an agent test environment from your own app** (optional, advanced): record a HAR of *your* CodeBench app at `http://app.codebench.localhost`, then:

```bash
rea environment-build ./captures/codebench.har ./agent-env/codebench --name codebench --json
agent-env run ./agent-env/codebench   # requires AgentEnv and Docker
```

That gives an AI agent a replayable copy of your API to practise tasks against, useful later for automated end-to-end testing.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Agent doesn't see REA tools | Re-run `npx rea-agents@latest setup`, then restart Claude Code |
| Tool list too large for the client | `REA_MCP_OUTPUT_SCHEMA_PROFILE=compact` (or `off`) in the MCP registration |
| JavaScript analysis fails on Linux | Install procps so `ps` is on REA's `PATH` |
| Node version error | Use Node 22.23.4+, 24.22.1+ or 26.12.1+ (e.g. via nvm) |
| Odd results after an update | `rea update`, refresh setup, retry; then open an issue with version + steps |

References: [REA README](https://github.com/morluto/rea), [rea.tools guides](https://rea.tools/guides/).
