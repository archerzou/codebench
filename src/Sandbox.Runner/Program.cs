// Phase 2 (docs/guide/03-worker-and-sandbox.md, docs/guide/09-seed-data-and-admin.md):
//   1. read bundle + user code from stdin (reject > 64 KB of code)
//   2. Roslyn compile against an allow-list of references
//   3. load into a collectible AssemblyLoadContext
//   4. run data-driven tests (visible first), measure time + allocations
//   5. emit one JSON result between nonce-delimited markers
await Console.Error.WriteLineAsync("CodeBench sandbox runner: not implemented yet.");
return 2;
