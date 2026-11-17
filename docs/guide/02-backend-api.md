# Backend API on the Vertical Slice template

The API keeps your template's conventions (one file per use case, `Result<T>`, no MediatR, direct `ApplicationDbContext`) and changes three things: Clerk replaces the template's own JWT issuing, NATS JetStream plus an outbox is added for grading, and Redis becomes the `HybridCache` second level.

## 1. Bootstrap from the template

```bash
git clone https://github.com/archerzou/Vertical_Slice_Architecture_Template codebench
cd codebench
rm -rf .git && git init
# In Claude Code, inside the repo:
/bootstrap            # rename solution to CodeBench, strip the Todos sample
```

Then move `src/Web.Api` and `tests/` into the monorepo layout from the Overview tab, and add the new projects:

```bash
dotnet new classlib -n CodeBench.Contracts -o src/Contracts
dotnet new worker   -n CodeBench.Worker    -o src/Worker
dotnet new console  -n CodeBench.Sandbox.Runner -o src/Sandbox.Runner
dotnet sln CodeBench.slnx add src/Contracts src/Worker src/Sandbox.Runner
dotnet add src/Web.Api reference src/Contracts
dotnet add src/Worker  reference src/Contracts
```

Add packages centrally in `Directory.Packages.props` (versions: pick the current stable at install time):

| Package | Project | Why |
| --- | --- | --- |
| `NATS.Net` | Web.Api, Worker | NATS client incl. JetStream |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | Web.Api | Redis as `HybridCache` L2 |
| `Svix` | Web.Api | Verify Clerk (Svix-signed) webhooks |
| `Docker.DotNet` | Worker | Talk to the sandbox Docker daemon |
| `Microsoft.CodeAnalysis.CSharp` | Sandbox.Runner | Roslyn compile + metrics |

Remove from Web.Api: the register, login and refresh-token slices, `TokenProvider`, refresh-token entity, and password hashing.

## 2. Authentication with Clerk

Create a Clerk application (free development instance). From the dashboard you need: the **Frontend API URL** (your token issuer, like `https://<something>.clerk.accounts.dev`), the **publishable key** (frontend), the **secret key** (backend, only if you call Clerk's Backend API), and a **webhook signing secret**.

`appsettings.json`:

```json
"Clerk": {
  "Issuer": "https://YOUR-INSTANCE.clerk.accounts.dev",
  "AuthorizedParties": ["http://app.codebench.localhost", "http://localhost:5173"],
  "WebhookSecret": ""
}
```

Put `WebhookSecret` in user-secrets locally and in a `.env` file for Docker; never commit it.

`Authentication/DependencyInjection.cs`:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var clerk = configuration.GetSection("Clerk");
        options.Authority = clerk["Issuer"];            // JWKS fetched from {Issuer}/.well-known/jwks.json
        options.MapInboundClaims = false;                // keep "sub" as "sub"
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,                    // Clerk session tokens have no aud by default
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                // Clerk puts the requesting origin in "azp"; reject tokens minted for other sites
                var azp = ctx.Principal?.FindFirst("azp")?.Value;
                var allowed = clerk.GetSection("AuthorizedParties").Get<string[]>() ?? [];
                if (azp is null || !allowed.Contains(azp, StringComparer.OrdinalIgnoreCase))
                {
                    ctx.Fail("Unauthorized party");
                }
                return Task.CompletedTask;
            }
        };
    });
```

`IUserContext` now exposes the Clerk id and resolves your local user id lazily:

```csharp
internal sealed class UserContext(IHttpContextAccessor accessor) : IUserContext
{
    public string ExternalId =>
        accessor.HttpContext?.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("No authenticated user");
}
```

In handlers, look up `db.Users.SingleAsync(u => u.ExternalId == userContext.ExternalId)`; cache the mapping in `HybridCache` with key `users:ext:{sub}`.

**Keeping users in sync:** a webhook slice receives `user.created`, `user.updated` and `user.deleted` from Clerk.

```csharp
public static class HandleClerkWebhook
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("webhooks/clerk", async (HttpRequest request, ApplicationDbContext db,
                    IConfiguration config, CancellationToken ct) =>
                {
                    using var reader = new StreamReader(request.Body);
                    var payload = await reader.ReadToEndAsync(ct);

                    var headers = new WebHeaderCollection
                    {
                        { "svix-id", request.Headers["svix-id"] },
                        { "svix-timestamp", request.Headers["svix-timestamp"] },
                        { "svix-signature", request.Headers["svix-signature"] }
                    };
                    new Svix.Webhook(config["Clerk:WebhookSecret"]).Verify(payload, headers); // throws if invalid

                    var evt = JsonSerializer.Deserialize<ClerkEvent>(payload)!;
                    switch (evt.Type)
                    {
                        case "user.created": db.Users.Add(User.FromClerk(evt.Data)); break;
                        case "user.updated": /* update email, name, avatar */ break;
                        case "user.deleted": /* soft-delete + anonymise submissions */ break;
                    }
                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                })
               .AllowAnonymous()
               .ExcludeFromDescription();
    }
}
```

Locally, Clerk can't reach `localhost`. Use the Clerk dashboard's webhook testing, a free tunnel (`cloudflared tunnel --url http://localhost:80`), or simply upsert the user on their first authenticated request (a small middleware) and treat the webhook as an optimisation.

**Authorization:** keep the template's permission model. Map plans to permissions in the database (`free` → `problems:algorithms`; `pro` → every track). Endpoints call `.HasPermission(Permissions.TrackDatabase)`. Paddle/Stripe webhooks later just change the user's plan.

## 3. Data model

| Entity | Key fields | Notes |
| --- | --- | --- |
| `User` | `Id`, `ExternalId` (Clerk `sub`, unique), `Email`, `Plan`, `CreatedAt` | Created by webhook or first request |
| `Problem` | `Id`, `Slug` (unique), `Title`, `Track`, `Difficulty`, `StatementMarkdown`, `StarterCode`, `GraderKind`, `Limits` (jsonb), `IsFree` | Seeded from `problems/` |
| `TestCase` | `Id`, `ProblemId`, `Name`, `IsHidden`, `InputJson`, `ExpectedJson`, `TimeBudgetMs`, `AllocBudgetBytes` | Hidden cases never returned by the API |
| `Submission` | `Id`, `UserId`, `ProblemId`, `Kind` (Run/Submit), `Code`, `Status`, `Verdict`, `CreatedAt`, `GradedAt` | Status: Pending → Running → Graded / Failed |
| `SubmissionTestResult` | `SubmissionId`, `TestName`, `Passed`, `ElapsedMicros`, `AllocatedBytes`, `Message`, `Details` (jsonb: SQL, plan, metrics) | One row per test |
| `UserProblemProgress` | `UserId`, `ProblemId`, `BestVerdict`, `Attempts`, `SolvedAt` | Drives catalog checkmarks, XP |
| `OutboxMessage` | `Id`, `Type`, `Payload` (jsonb), `OccurredAt`, `ProcessedAt`, `Error` | Transactional outbox |

Configure each in `Database/Configurations/` (snake_case is already on). Add an index on `outbox_messages (processed_at) WHERE processed_at IS NULL` and on `submissions (user_id, created_at DESC)`. Use `/add-entity` and `/add-migration` to scaffold.

## 4. Message contracts (src/Contracts)

```csharp
namespace CodeBench.Contracts;

public static class Subjects
{
    public const string RunsCreated        = "jobs.runs.created";
    public const string SubmissionsCreated = "jobs.submissions.created";
    public const string Graded             = "results.graded";
}

public sealed record GradeRequested(
    Guid SubmissionId, string ProblemSlug, string GraderKind, bool IncludeHidden,
    string Code, int TimeoutSeconds, int MemoryMb);

public sealed record TestOutcome(
    string Name, bool Passed, long ElapsedMicros, long AllocatedBytes,
    string? Message, JsonElement? Details);

public sealed record GradeCompleted(
    Guid SubmissionId, string Verdict,          // Accepted, WrongAnswer, CompileError, Timeout, BudgetExceeded, InternalError
    string? CompilerOutput, IReadOnlyList<TestOutcome> Tests, DateTimeOffset CompletedAt);
```

The worker sends the problem **slug**, not the hidden tests. Hidden tests live inside the sandbox image (or a read-only volume), so they never travel over NATS or reach the browser.

## 5. Slices

| Feature folder | Slice | Route | Auth |
| --- | --- | --- | --- |
| Problems | `GetProblems` | `GET /problems` (search, track, difficulty, page) | Anonymous |
| Problems | `GetProblem` | `GET /problems/{slug}` | Anonymous; Pro tracks need permission |
| Submissions | `CreateRun` | `POST /runs` | Anonymous allowed for free problems, strict rate limit |
| Submissions | `CreateSubmission` | `POST /submissions` | Signed in |
| Submissions | `GetSubmission` | `GET /submissions/{id}` | Owner only |
| Submissions | `GetMySubmissions` | `GET /me/submissions` | Signed in |
| Progress | `GetProgress` | `GET /me/progress` | Signed in |
| Users | `HandleClerkWebhook` | `POST /webhooks/clerk` | Signature |
| Users | `GetMe` | `GET /me` | Signed in |

`CreateSubmission`, the core write path:

```csharp
public static class CreateSubmission
{
    public sealed record Command(Guid ProblemId, string Code) : ICommand<Guid>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.ProblemId).NotEmpty();
            RuleFor(c => c.Code).NotEmpty().MaximumLength(64_000);
        }
    }

    internal sealed class Handler(ApplicationDbContext db, IUserContext user, TimeProvider clock)
        : ICommandHandler<Command, Guid>
    {
        public async Task<Result<Guid>> Handle(Command cmd, CancellationToken ct)
        {
            var me = await db.Users.SingleAsync(u => u.ExternalId == user.ExternalId, ct);
            var problem = await db.Problems.AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == cmd.ProblemId, ct);
            if (problem is null) return Result.Failure<Guid>(ProblemErrors.NotFound(cmd.ProblemId));

            var pending = await db.Submissions.CountAsync(
                s => s.UserId == me.Id && s.Status == SubmissionStatus.Pending, ct);
            if (pending >= 2) return Result.Failure<Guid>(SubmissionErrors.TooManyInFlight);

            var submission = Submission.Create(me.Id, problem.Id, SubmissionKind.Submit, cmd.Code, clock.GetUtcNow());
            db.Submissions.Add(submission);
            db.OutboxMessages.Add(OutboxMessage.Create(Subjects.SubmissionsCreated,
                new GradeRequested(submission.Id, problem.Slug, problem.GraderKind, IncludeHidden: true,
                                   cmd.Code, problem.Limits.TimeoutSeconds, problem.Limits.MemoryMb),
                clock.GetUtcNow()));

            await db.SaveChangesAsync(ct);   // one transaction: submission + outbox row
            return submission.Id;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("submissions", async (Command cmd, ICommandHandler<Command, Guid> handler, CancellationToken ct) =>
                    (await handler.Handle(cmd, ct)).Match(
                        id => Results.Accepted($"/submissions/{id}", new { id }),
                        CustomResults.Problem))
               .WithTags(Tags.Submissions)
               .WithIdempotency()
               .RequireAuthorization()
               .RequireRateLimiting("submissions");
    }
}
```

`GetSubmission` returns status, verdict and per-test results; for hidden tests return only `Name`, `Passed`, timings and a generic message, never inputs or expected output.

## 6. NATS JetStream setup

Two work-queue streams keep jobs and results separate, so each has exactly one consumer group:

| Stream | Subjects | Consumer (durable) | Who consumes |
| --- | --- | --- | --- |
| `JOBS` | `jobs.>` | `graders` | Worker(s), competing |
| `RESULTS` | `results.>` | `api-results` | API instance(s), competing |

Create them at startup (idempotent) in a hosted service of the API:

```csharp
await using var nats = new NatsClient(configuration.GetConnectionString("Nats")!);
var js = nats.CreateJetStreamContext();

await js.CreateOrUpdateStreamAsync(new StreamConfig("JOBS", ["jobs.>"])
{
    Retention = StreamConfigRetention.Workqueue,
    MaxAge = TimeSpan.FromHours(1),
    Storage = StreamConfigStorage.File
});
await js.CreateOrUpdateStreamAsync(new StreamConfig("RESULTS", ["results.>"])
{
    Retention = StreamConfigRetention.Workqueue,
    Storage = StreamConfigStorage.File
});
```

Register one `NatsClient` as a singleton in DI rather than creating one per call. Verify method names against the current NATS.Net docs when you install; the v2 API is stable but evolves.

## 7. Outbox publisher and results consumer

```csharp
// Messaging/OutboxPublisher.cs — a BackgroundService (scaffold with /add-background-job)
internal sealed class OutboxPublisher(IServiceScopeFactory scopes, INatsJSContext js,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(ct))
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var batch = await db.OutboxMessages
                .FromSql($"SELECT * FROM outbox_messages WHERE processed_at IS NULL ORDER BY occurred_at LIMIT 50 FOR UPDATE SKIP LOCKED")
                .ToListAsync(ct);

            foreach (var msg in batch)
            {
                try
                {
                    var ack = await js.PublishAsync(msg.Type, msg.Payload,
                        opts: new NatsJSPubOpts { MsgId = msg.Id.ToString() }, cancellationToken: ct); // MsgId = dedupe
                    ack.EnsureSuccess();
                    msg.ProcessedAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    msg.Error = ex.Message;
                    logger.LogWarning(ex, "Outbox publish failed for {Id}", msg.Id);
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
```

Wrap the select and save in one transaction so `SKIP LOCKED` protects against two API instances publishing the same row; JetStream's `MsgId` de-duplication covers the rest.

The results consumer (`Messaging/GradeResultConsumer.cs`) pulls `GradeCompleted` from the `api-results` consumer, updates `Submission` and `SubmissionTestResult` rows, updates `UserProblemProgress`, removes `progress:{userId}` from `HybridCache`, then acks. Make it idempotent: if the submission is already `Graded`, just ack.

## 8. Caching, rate limits, CORS

```csharp
services.AddStackExchangeRedisCache(o => o.Configuration = configuration.GetConnectionString("Redis"));
services.AddHybridCache();   // picks up the Redis IDistributedCache as L2
```

Cache `GetProblems` pages (`problems:list:{hash}`) and `GetProblem` (`problems:{slug}`) for 10 minutes; invalidate on seeding.

Rate limiting (template's `RateLimiting` section): add a `submissions` policy partitioned by user id (e.g. 10 per minute) and an `anonymous-runs` policy partitioned by IP (e.g. 5 per minute). CORS `AllowedOrigins`: `http://codebench.localhost`, `http://app.codebench.localhost`, `http://localhost:5173`.

## 9. Seeding problems

Each folder in `problems/` has a `problem.yaml`:

```yaml
slug: two-sum
title: Two Sum
track: algorithms
difficulty: easy
grader: algorithms
free: true
limits: { timeoutSeconds: 10, memoryMb: 256 }
signature: "public int[] TwoSum(int[] nums, int target)"
tests:
  - { name: Classic example, file: Tests.Visible.cs, hidden: false, timeBudgetMs: 50 }
  - { name: Large input O(n), file: Tests.Hidden.cs, hidden: true, timeBudgetMs: 200, allocBudgetBytes: 1048576 }
```

A `SeedProblems` slice (admin-only, or a `--seed` startup flag) reads the YAML and upserts `Problem` and `TestCase` rows. Write your own problems; classic algorithm problems are fine to implement, but write the statements and tests yourself.

## 10. Tests

- **Unit:** validators and handlers with NSubstitute (template pattern).
- **Integration:** Testcontainers for Postgres **and** NATS (`nats:2` image with `-js`); assert that `POST /submissions` writes a submission plus an outbox row, and that a fake `GradeCompleted` updates progress.
- **Architecture (NetArchTest):** add rules that `CodeBench.Worker` has no dependency on `Microsoft.EntityFrameworkCore` or `Npgsql`, and that `Contracts` depends on nothing but the BCL.
- **Auth:** in tests, replace Clerk with a test JWT handler that issues tokens with `sub` and `azp` claims.
