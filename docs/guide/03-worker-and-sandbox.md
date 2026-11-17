# Worker and sandbox: running untrusted C# safely

The worker never executes user code itself: it starts one throwaway container per submission on a separate Docker daemon, with no network, capped CPU, memory, processes and time, and reads back a JSON result. Assume every submission is an escape attempt.

## Threat model

| Threat | Example in submitted code | Control |
| --- | --- | --- |
| Data theft | Read env vars, connection strings, other users' code | Worker holds no DB credentials; sandbox gets only the code and its own test files |
| Network abuse | `HttpClient` to the internet, port scans, crypto mining pools | `NetworkMode = none` on every sandbox |
| Resource exhaustion | `while(true)`, fork bombs, 10 GB allocations, disk filling | CPU quota, memory limit without swap, `PidsLimit`, read-only root, size-capped `tmpfs`, wall-clock timeout |
| Host compromise | Container escape via kernel bug or Docker socket | Separate DinD daemon (the host socket is never mounted into the worker), non-root user, all capabilities dropped, `no-new-privileges`, gVisor where available |
| Result forgery | Printing a fake "all passed" JSON | Harness writes results to a file descriptor or a nonce-delimited block the user code doesn't know; worker validates the schema |
| Cheating | Reading hidden test files, reflection on the harness | Hidden tests compiled into the harness, loaded after user code; user code runs in a separate collectible `AssemblyLoadContext` |

## Topology in Docker Compose

- `worker` joins `backend` (to reach NATS) and `exec` (to reach the sandbox daemon). It gets `NATS_URL` and `DOCKER_HOST=tcp://sandbox-daemon:2375`, nothing else.
- `sandbox-daemon` is the official `docker:dind` image, `privileged: true` (DinD requires it), joined to `exec` (internal, so only the worker can talk to it) and `egress` (so the daemon itself can pull sandbox images from GHCR).
- Sandboxes are started *by* that daemon with `NetworkMode = none`, so the `egress` network never reaches user code.
- TLS on the DinD socket is disabled (`DOCKER_TLS_CERTDIR: ""`) because the `exec` network is internal and the worker is its only other member. Turn TLS on if you ever add anything to that network.

Why not mount `/var/run/docker.sock` into the worker? Anyone who controls that socket controls your host as root. With DinD, a compromised worker controls only the nested daemon.

**Stronger isolation (Linux only):** install [gVisor](https://gvisor.dev/docs/user_guide/install/) and register `runsc` as a runtime on the daemon that starts sandboxes, then set `HostConfig.Runtime = "runsc"`. Docker Desktop on Windows/macOS can't do this; use WSL2 with native Docker Engine if you want it. Test it before relying on it.

## Sandbox images

One image per grader kind, built in CI and pulled by the sandbox daemon. The harness is published self-contained so start-up is fast.

`sandbox-images/algorithms.Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Sandbox.Runner/ Sandbox.Runner/
COPY problems/ problems/
RUN dotnet publish Sandbox.Runner -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN useradd --uid 10001 --no-create-home --shell /usr/sbin/nologin runner
COPY --from=build /out /opt/runner
COPY --from=build /src/problems /opt/problems
USER 10001
WORKDIR /tmp
ENTRYPOINT ["dotnet", "/opt/runner/Sandbox.Runner.dll", "--grader", "algorithms"]
```

The runner compiles user code in-process with Roslyn, so the image needs the runtime plus Roslyn assemblies (pulled in by the runner's `Microsoft.CodeAnalysis.CSharp` package), not the full SDK. `database.Dockerfile` adds a PostgreSQL server; see the Database grader section.

## The worker consumer

```csharp
internal sealed class SubmissionConsumer(INatsJSContext js, SandboxRunner sandbox,
    ILogger<SubmissionConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var consumer = await js.CreateOrUpdateConsumerAsync("JOBS", new ConsumerConfig("graders")
        {
            FilterSubjects = [Subjects.RunsCreated, Subjects.SubmissionsCreated],
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
            AckWait = TimeSpan.FromMinutes(2),
            MaxDeliver = 3,
            MaxAckPending = 4          // = max parallel sandboxes per worker
        }, ct);

        await foreach (var msg in consumer.ConsumeAsync<GradeRequested>(cancellationToken: ct))
        {
            var request = msg.Data!;
            GradeCompleted result;
            try
            {
                result = await sandbox.RunAsync(request, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sandbox failed for {SubmissionId}", request.SubmissionId);
                result = GradeCompleted.InternalError(request.SubmissionId);
            }

            await js.PublishAsync(Subjects.Graded, result,
                opts: new NatsJSPubOpts { MsgId = $"graded-{request.SubmissionId}" }, cancellationToken: ct);
            await msg.AckAsync(cancellationToken: ct);
        }
    }
}
```

Process messages in parallel up to `MaxAckPending` with a `Channel` or `Parallel.ForEachAsync` once the single-threaded version works.

## Starting a sandbox (Docker.DotNet)

```csharp
internal sealed class SandboxRunner(DockerClient docker, IOptions<SandboxOptions> options)
{
    public async Task<GradeCompleted> RunAsync(GradeRequested req, CancellationToken ct)
    {
        var image = options.Value.Images[req.GraderKind];   // e.g. ghcr.io/you/codebench-sandbox-algorithms:sha
        var create = await docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = image,
            Cmd = ["--problem", req.ProblemSlug, "--hidden", req.IncludeHidden.ToString()],
            User = "10001:10001",
            AttachStdin = true, OpenStdin = true, StdinOnce = true,
            NetworkDisabled = true,
            HostConfig = new HostConfig
            {
                NetworkMode = "none",
                Memory = req.MemoryMb * 1024L * 1024L,
                MemorySwap = req.MemoryMb * 1024L * 1024L,     // no swap
                NanoCPUs = 1_000_000_000,                       // 1 CPU
                PidsLimit = 128,
                ReadonlyRootfs = true,
                Tmpfs = new Dictionary<string, string> { ["/tmp"] = "rw,size=64m,noexec,nosuid" },
                CapDrop = ["ALL"],
                SecurityOpt = ["no-new-privileges"],
                Runtime = options.Value.Runtime,                // "runc" by default, "runsc" with gVisor
                AutoRemove = false
            },
            Labels = new Dictionary<string, string> { ["codebench.submission"] = req.SubmissionId.ToString() }
        }, ct);

        try
        {
            // 1. attach, write the user code to stdin, close stdin
            // 2. start the container
            // 3. wait with a wall-clock timeout
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(req.TimeoutSeconds));
            // ... await docker.Containers.WaitContainerAsync(create.ID, timeout.Token)
            // on timeout: KillContainerAsync, return Verdict = "Timeout"
            // 4. read stdout (cap at 1 MB), extract the nonce-delimited JSON, validate, map to GradeCompleted
            return await ReadResultAsync(create.ID, req, ct);
        }
        finally
        {
            await docker.Containers.RemoveContainerAsync(create.ID,
                new ContainerRemoveParameters { Force = true }, CancellationToken.None);
        }
    }
}
```

Also run a janitor every minute that force-removes containers labelled `codebench.submission` older than five minutes, in case a worker crashed mid-run.

## The grader harness (Sandbox.Runner)

Shared flow for every grader kind:

1. Read user code from stdin (reject over 64 KB).
2. Compile with Roslyn into an in-memory assembly, referencing only an allow-list of assemblies (`System.Runtime`, `System.Collections`, `System.Linq`, plus EF Core for database problems). On failure, emit `CompileError` with diagnostics.
3. Load the assembly into a **collectible** `AssemblyLoadContext`.
4. Load the problem's tests (compiled into the runner image) and run each in order, visible first.
5. Measure, compare against budgets, and emit one JSON result between two lines containing a per-run nonce the user code can't predict, e.g. a GUID generated before user code loads.

Compiling:

```csharp
var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
var compilation = CSharpCompilation.Create(
    "Submission", [tree], AllowedReferences.For(problem),
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
        optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Enable));

using var pe = new MemoryStream();
var emit = compilation.Emit(pe);
if (!emit.Success) return Result.CompileError(emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

pe.Position = 0;
var alc = new AssemblyLoadContext("submission", isCollectible: true);
var assembly = alc.LoadFromStream(pe);
```

The reference allow-list is a convenience, not a security boundary: user code can still use reflection or P/Invoke. The container is the boundary.

## Algorithms grader: time and allocations

```csharp
static Measurement Measure(Func<object?> invoke, int runs = 7)
{
    invoke();                                   // warm-up: JIT, tiered compilation
    var times = new long[runs];
    long alloc = 0;
    for (var i = 0; i < runs; i++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        invoke();
        sw.Stop();
        alloc = GC.GetAllocatedBytesForCurrentThread() - before;
        times[i] = sw.Elapsed.Ticks;
    }
    Array.Sort(times);
    return new Measurement(ElapsedMicros: times[runs / 2] / 10, AllocatedBytes: alloc);  // median
}
```

Each test then checks correctness, `ElapsedMicros <= TimeBudgetMs * 1000`, and `AllocatedBytes <= AllocBudgetBytes`. Use a hidden large input (e.g. 200,000 elements) so an O(n²) solution fails the time budget. Run tests on one thread so `GetAllocatedBytesForCurrentThread` is meaningful, and set `DOTNET_TieredPGO=0` in the image if timings vary too much between runs.

## Database grader: real Postgres and query plans

The database sandbox image adds a PostgreSQL server that starts inside the container, so it shares the sandbox's no-network isolation.

```dockerfile
FROM codebench-sandbox-base AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends postgresql && rm -rf /var/lib/apt/lists/*
COPY sandbox-images/db-entrypoint.sh /opt/db-entrypoint.sh
USER 10001
ENTRYPOINT ["/opt/db-entrypoint.sh"]   # initdb into /tmp, start postgres on a Unix socket, then exec the runner
```

Because the root filesystem is read-only, `initdb` and the data directory go into the `tmpfs` (`/tmp/pgdata`); raise that `tmpfs` to around 256 MB for these images. For faster start-up, bake a pre-initialised, pre-seeded data directory into the image and copy it into `/tmp` at start.

In the harness:

1. Seed the problem's schema and data (tens of thousands of rows so the planner behaves realistically), then `ANALYZE`.
2. Build a `DbContext` from the problem's model, with a `DbCommandInterceptor` that records every SQL statement and parameters.
3. Invoke the user's method and compare results with the expected rows.
4. For each captured statement, run `EXPLAIN (FORMAT JSON) <sql>` with the same parameters and walk the plan tree.
5. Evaluate the problem's plan rules and return the SQL, the plan summary and rule outcomes in `Details`.

Example rule set in `problem.yaml`:

```yaml
planRules:
  - { id: no-seq-scan,  type: noSeqScan,  table: taps }
  - { id: uses-index,   type: usesIndex,  index: ix_taps_tapped_at }
  - { id: one-trip,     type: maxRoundTrips, value: 1 }
```

Rule checks are simple walks over the JSON plan: `"Node Type": "Seq Scan"` with `"Relation Name": "taps"` fails `noSeqScan`; any node with `"Index Name": "ix_taps_tapped_at"` passes `usesIndex`; the interceptor's statement count checks round trips.

## Other graders (later phases)

| Grader | How it works | Key tool |
| --- | --- | --- |
| Refactoring | Run behaviour tests; then compute cyclomatic complexity, max nesting depth, method length and duplicate blocks from the syntax tree, and compare with thresholds | Roslyn `CSharpSyntaxWalker` |
| Architecture | Multi-file submission compiled into separate assemblies; assert dependency rules on the compiled types | NetArchTest (already in your template) |
| Secure coding | Run normal tests plus an adversarial suite (path traversal strings, SQL injection payloads) against the user's method; both must pass | xUnit-style cases in the runner |
| Design principles | Assert structure via reflection: types `sealed`, properties `init`-only, no public setters on aggregates | `System.Reflection` |
| Test writing | Run the user's tests against the correct implementation (must pass) and N planted-bug mutants (must fail enough of them) | Mutants pre-built into the image |

## Hardening checklist

- [ ] Sandbox containers: `NetworkMode none`, `ReadonlyRootfs`, `CapDrop ALL`, `no-new-privileges`, non-root user, `PidsLimit`, memory = swap limit, CPU quota.
- [ ] Wall-clock timeout enforced by the worker (not only inside the container) and container force-removed in `finally`.
- [ ] Output capped (e.g. 1 MB) and parsed only from the nonce-delimited block; schema-validated.
- [ ] Worker image contains no Postgres client libraries and no connection strings (verify with REA, see the REA guide tab).
- [ ] Host Docker socket never mounted anywhere except, if you must, a CI runner.
- [ ] Janitor removes stale sandbox containers; disk usage of the DinD volume monitored in Grafana.
- [ ] Per-user in-flight limit (API) and per-worker `MaxAckPending` (NATS).
- [ ] A folder of hostile test submissions you wrote yourself (socket open, file write outside `/tmp`, fork bomb, 2 GB allocation, infinite loop, fake result JSON) that CI runs against every sandbox image and expects the right verdicts.
