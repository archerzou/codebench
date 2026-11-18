# Recon notes

**Outcome: no recon of Katabench was performed.** Reviewing its Terms (step 0.4.1) showed that the
planned observation would conflict with them, so CodeBench is designed from its own requirements
(`docs/guide/`, `docs/api-contract.md`) instead. Decision: [ADR 0006](adr/0006-no-katabench-recon.md).

## Terms review (2026-11-18)

Read: https://katabench.com/legal/terms and https://katabench.com/legal/acceptable-use, both
"Last updated: November 11, 2026". The Terms incorporate the Acceptable Use Policy.

| Clause | Gist | Effect on the planned recon |
| --- | --- | --- |
| Terms §2 | May not "use the Service or its content to build, train, or benchmark a competing product or model" | CodeBench is a competing practice platform, and the recon's stated purpose was to shape it → **conflicts outright** |
| Terms §2 | May not "reverse-engineer, scrape, or bulk-extract puzzles, test suites, or reference solutions" | Mapping their app's endpoints and response shapes with REA is plausibly reverse-engineering |
| AUP | "no scripted scraping of the API or puzzle content" | REA browser scenarios are scripted observation of their API |
| AUP | "No attempts to escape, probe, or disable the sandbox" | Recording the Run flow's shapes is borderline "probe"; avoided entirely |
| Terms §3 / AUP | One account per person; no cycling accounts to dodge quotas | Not relevant once recon is dropped |
| Terms §5 | Puzzles, tests, solutions, site content and the platform are their IP | Already a rule here: nothing copied |

The pages don't define "reverse-engineer" or "probe", but the competing-product clause is
unambiguous on its own. Written permission from Katabench would be the only route to reopening this.

## What replaces the recon

- **Features** come from the guide (00, 07, 09) and are listed in [feature-map.md](feature-map.md);
  the "observed" column is retired.
- **The API** is our own design in [api-contract.md](api-contract.md), reviewed 2026-11-18 against
  build plan §1.6 / §2.2, guide 02 and 09, and `src/Contracts`.
- **REA** is kept for the part of guide 01 that targets only our own builds: web bundle audit and
  Worker assembly audit (Phase 4.4). The CLI (`rea` 6.7.0) is installed; MCP registration
  (`npx rea-agents setup`) is deferred until then.
- General, public knowledge of how online judges work (Monaco editors, per-test results, polling vs
  push) needs no observation of any particular product.
