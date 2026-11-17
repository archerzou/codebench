# Seed data and admin console

CodeBench starts with six original sample problems (four algorithms, one EF Core, one refactoring), an admin account bootstrapped from your email, and an `/admin` area in the React app for managing problems, test cases, users and submissions. Problems move from YAML seed files into the database, so the admin can edit them without a rebuild.

## What changes in earlier tabs

Making content editable at runtime changes three earlier decisions:

| Before | Now | Why |
| --- | --- | --- |
| Hidden tests baked into sandbox images from `problems/` | Tests stored in Postgres; on publish, the API writes a versioned **problem bundle** to a NATS Object Store bucket; the worker fetches and caches it | Admin edits take effect without rebuilding images; the worker still never touches the database |
| Tests written as C# files per problem | Most tests are **data**: JSON arguments + expected value + comparison mode, run by a generic harness | CRUD forms can edit them; C# test code stays available for advanced graders |
| `problems/*.yaml` is the source of truth | YAML is only the **initial seed**; the database is the source of truth afterwards | Admin changes would otherwise be overwritten |

`GradeRequested` gains `ProblemVersion`, and the sandbox receives the bundle on stdin together with the user code. Hidden tests still never reach the browser.

## Roles and the admin account

The role lives in **your** database, not in Clerk, so authorization stays in one place and works with your template's permission system.

| Role | Permissions |
| --- | --- |
| `learner` (default) | `problems:read`, `submissions:create`, `submissions:read-own` |
| `author` | learner + `admin:access`, `problems:write`, `tests:write` |
| `admin` | author + `problems:publish`, `problems:delete`, `users:manage`, `submissions:read-all`, `stats:read` |

Add `Role` to `User` and a `RolePermissions` table seeded with the matrix above. `PermissionProvider` (already in the template) loads the user's permissions by `ExternalId`, cached in `HybridCache` for five minutes; invalidate when a role changes.

**Bootstrapping the first admin** without a chicken-and-egg problem:

```json
// appsettings.json
"Admin": {
  "BootstrapEmails": [ ]   // set via env: Admin__BootstrapEmails__0=you@example.com
}
```

When a user is created (Clerk webhook or first-request upsert) and their verified email is in `Admin:BootstrapEmails`, they get the `admin` role. After that, promote others from the Users page. Add `ADMIN_BOOTSTRAP_EMAIL=` to `deploy/.env` and map it to `Admin__BootstrapEmails__0` on the `api` service.

**Optional UI hint from Clerk:** set `publicMetadata.role = "admin"` on your Clerk user and add `{"role": "{{user.public_metadata.role}}"}` to the session token template (Clerk dashboard → Sessions → Customize session token). The React app can then show the Admin link without an extra request. The API never trusts this claim for authorization; it always checks the database.

## Data model additions

| Entity / field | Purpose |
| --- | --- |
| `User.Role` | `learner`, `author`, `admin` |
| `User.IsDisabled` | Blocks API access (checked in the permission handler) |
| `User.IsDemo` | Marks seeded demo learners so they can be wiped |
| `Problem.Status` | `Draft` → `Published` → `Archived` |
| `Problem.Version` | Incremented on every publish; submissions record the version they ran against |
| `Problem.ReferenceSolution` | Admin-only; used by "Validate" before publishing |
| `Problem.Signature` | Method the harness calls, e.g. `public long SumOfEvens(int[] nums)` |
| `Problem.Tags`, `Problem.SortOrder` | Catalog filters and learning-path order |
| `TestCase.ArgsJson`, `ExpectedJson`, `Compare` | Data-driven test: arguments, expected value, comparison mode |
| `TestCase.Generator` | Optional named generator for large performance inputs |
| `AuditLog` | `Id`, `ActorUserId`, `Action`, `EntityType`, `EntityId`, `BeforeJson`, `AfterJson`, `At` |

Use optimistic concurrency on `Problem` (Postgres `xmin` as a concurrency token) so two admins editing the same problem get a 409 instead of a silent overwrite.

## Data-driven test format

```json
{
  "name": "Mixed signs",
  "hidden": false,
  "args": [[-2, -3, 7, 10]],
  "expected": 8,
  "compare": "exact",
  "timeBudgetMs": 50,
  "allocBudgetBytes": null
}
```

| `compare` | Meaning |
| --- | --- |
| `exact` | JSON-equal after serialising the return value |
| `unordered` | Same elements in any order (top-level list) |
| `unorderedNested` | Unordered outer list, ordered inner lists (e.g. intervals) |
| `approx` | Doubles within `1e-6` |

For big inputs, `args` can hold a generator instead of literal data, so test rows stay small:

```json
{ "name": "200k elements", "hidden": true,
  "args": [{ "$gen": "randomInts", "count": 200000, "min": -1000, "max": 1000, "seed": 42 }],
  "expected": { "$ref": "reference" },
  "timeBudgetMs": 30, "allocBudgetBytes": 1024 }
```

`"expected": {"$ref": "reference"}` means "compute it by running the reference solution on the same input". The harness ships a few generators (`randomInts`, `randomStrings`, `randomIntervals`, `sortedInts`) keyed by name with a fixed seed, so results are reproducible.

The harness finds `public class Solution`, locates the method named in `Problem.Signature`, deserialises each argument with `System.Text.Json` into the parameter's type, invokes it inside the measurement loop from the Worker & Sandbox tab, then serialises the result and compares.

## The six seed problems

All original and deliberately simple, one per grader path, so you can test the whole pipeline. Files live in `problems/<slug>/problem.yaml` (seed data only — loaded once by the seeder).

| # | Slug | Track | Difficulty | Free | Exercises |
| --- | --- | --- | --- | --- | --- |
| 1 | `sum-of-evens` | Algorithms | Easy | Yes | Basic pipeline, allocation budget (no LINQ allocations) |
| 2 | `first-unique-char` | Algorithms | Easy | Yes | String handling, O(n) time budget |
| 3 | `merge-intervals` | Algorithms | Medium | Yes | `unorderedNested` compare, generator input |
| 4 | `top-k-words` | Algorithms | Medium | No | Sorting with tie-break, `exact` compare |
| 5 | `customers-since` | EF Core | Medium | No | Plan rules: index use, one round trip |
| 6 | `shipping-cost-refactor` | Refactoring | Easy | Yes | Behaviour tests + complexity and nesting thresholds |

### 1. sum-of-evens

```yaml
slug: sum-of-evens
title: Sum of Evens
track: algorithms
difficulty: easy
free: true
tags: [arrays, allocations]
signature: "public long SumOfEvens(int[] nums)"
statement: |
  Return the sum of all even numbers in `nums`. Return 0 for an empty array.
  The hidden performance test has a 1 KB allocation budget, so avoid creating
  new collections.
starter: |
  public class Solution
  {
      public long SumOfEvens(int[] nums)
      {
          throw new NotImplementedException();
      }
  }
reference: |
  public class Solution
  {
      public long SumOfEvens(int[] nums)
      {
          long sum = 0;
          foreach (var n in nums) if ((n & 1) == 0) sum += n;
          return sum;
      }
  }
limits: { timeoutSeconds: 10, memoryMb: 256 }
tests:
  - { name: Example, args: [[1, 2, 3, 4]], expected: 6 }
  - { name: Empty, args: [[]], expected: 0 }
  - { name: Mixed signs, args: [[-2, -3, 7, 10]], expected: 8 }
  - { name: All odd, hidden: true, args: [[1, 3, 5]], expected: 0 }
  - name: 200k elements
    hidden: true
    args: [{ $gen: randomInts, count: 200000, min: -1000, max: 1000, seed: 42 }]
    expected: { $ref: reference }
    timeBudgetMs: 30
    allocBudgetBytes: 1024
```

A LINQ answer like `nums.Where(n => n % 2 == 0).Sum()` passes correctness but allocates an iterator and a delegate, which is a good first lesson in what the allocation budget measures. Calibrate budgets on your machine by running the reference solution first (the Validate button does this).

### 2. first-unique-char

```yaml
slug: first-unique-char
title: First Unique Character
track: algorithms
difficulty: easy
free: true
tags: [strings, hashing]
signature: "public int FirstUniqueIndex(string s)"
statement: |
  Return the index of the first character in `s` that appears exactly once,
  or -1 if there is none. `s` contains only lowercase English letters.
starter: |
  public class Solution
  {
      public int FirstUniqueIndex(string s) => throw new NotImplementedException();
  }
reference: |
  public class Solution
  {
      public int FirstUniqueIndex(string s)
      {
          Span<int> counts = stackalloc int[26];
          foreach (var c in s) counts[c - 'a']++;
          for (var i = 0; i < s.Length; i++) if (counts[s[i] - 'a'] == 1) return i;
          return -1;
      }
  }
tests:
  - { name: First char unique, args: ["banana"], expected: 0 }
  - { name: Middle, args: ["aabbcdd"], expected: 4 }
  - { name: None, args: ["aabb"], expected: -1 }
  - { name: Single, hidden: true, args: ["z"], expected: 0 }
  - name: Long string
    hidden: true
    args: [{ $gen: randomStrings, length: 100000, alphabet: "abcdefghijklmnopqrstuvwxyz", seed: 7 }]
    expected: { $ref: reference }
    timeBudgetMs: 20
```

### 3. merge-intervals

```yaml
slug: merge-intervals
title: Merge Booking Windows
track: algorithms
difficulty: medium
free: true
tags: [sorting, intervals]
signature: "public int[][] Merge(int[][] windows)"
statement: |
  Each window is [start, end] with start <= end. Merge all windows that overlap
  or touch (end == next start) and return the merged windows sorted by start.
reference: |
  public class Solution
  {
      public int[][] Merge(int[][] windows)
      {
          if (windows.Length == 0) return [];
          Array.Sort(windows, (a, b) => a[0].CompareTo(b[0]));
          var result = new List<int[]> { windows[0] };
          foreach (var w in windows.AsSpan(1))
          {
              var last = result[^1];
              if (w[0] <= last[1]) last[1] = Math.Max(last[1], w[1]);
              else result.Add(w);
          }
          return result.ToArray();
      }
  }
tests:
  - { name: Overlap, args: [[[1, 3], [2, 6], [8, 10]]], expected: [[1, 6], [8, 10]] }
  - { name: Touching, args: [[[1, 4], [4, 5]]], expected: [[1, 5]] }
  - { name: Unsorted input, args: [[[5, 7], [1, 2]]], expected: [[1, 2], [5, 7]] }
  - { name: Empty, hidden: true, args: [[]], expected: [] }
  - name: 50k windows
    hidden: true
    args: [{ $gen: randomIntervals, count: 50000, maxStart: 1000000, maxLength: 50, seed: 3 }]
    expected: { $ref: reference }
    timeBudgetMs: 60
```

The starter is the same shape as problem 2's (`throw new NotImplementedException()`), omitted here for brevity.

### 4. top-k-words

```yaml
slug: top-k-words
title: Top K Words
track: algorithms
difficulty: medium
free: false
tags: [hashing, sorting]
signature: "public IList<string> TopK(string[] words, int k)"
statement: |
  Return the k most frequent words, highest count first. Break ties
  alphabetically (ordinal). 1 <= k <= number of distinct words.
reference: |
  public class Solution
  {
      public IList<string> TopK(string[] words, int k)
      {
          var counts = new Dictionary<string, int>(StringComparer.Ordinal);
          foreach (var w in words) counts[w] = counts.GetValueOrDefault(w) + 1;
          return counts.OrderByDescending(p => p.Value)
                       .ThenBy(p => p.Key, StringComparer.Ordinal)
                       .Take(k).Select(p => p.Key).ToList();
      }
  }
tests:
  - { name: Example, args: [["go", "net", "go", "sql", "net", "go"], 2], expected: ["go", "net"] }
  - { name: Tie break, args: [["b", "a", "b", "a", "c"], 2], expected: ["a", "b"] }
  - { name: k equals distinct, hidden: true, args: [["x", "y"], 2], expected: ["x", "y"] }
```

### 5. customers-since (EF Core)

Database problems add a schema and seed SQL. The bundle carries them; the database sandbox runs them before the tests.

```yaml
slug: customers-since
title: Customers Who Ordered Recently
track: database
difficulty: medium
free: false
grader: database
signature: "public Task<List<string>> GetNames(ShopDb db, DateTime since)"
statement: |
  Return the distinct names of customers with at least one order placed on or
  after `since`, sorted alphabetically. Use one query, and let Postgres use the
  index on orders.placed_at.
schema: |
  CREATE TABLE customers (id int PRIMARY KEY, name text NOT NULL);
  CREATE TABLE orders (id int PRIMARY KEY, customer_id int NOT NULL REFERENCES customers(id),
                       placed_at timestamptz NOT NULL, total numeric(10,2) NOT NULL);
  CREATE INDEX ix_orders_placed_at ON orders (placed_at);
seedSql: |
  INSERT INTO customers SELECT g, 'Customer ' || lpad(g::text, 5, '0') FROM generate_series(1, 5000) g;
  INSERT INTO orders SELECT g, 1 + (g % 5000), timestamptz '2026-01-01' + (g || ' minutes')::interval, 10
  FROM generate_series(1, 200000) g;
  ANALYZE;
model: |
  // compiled into the harness, visible to the user as read-only context
  public class Customer { public int Id { get; set; } public string Name { get; set; } = ""; public List<Order> Orders { get; set; } = []; }
  public class Order { public int Id { get; set; } public int CustomerId { get; set; } public DateTime PlacedAt { get; set; } public decimal Total { get; set; } }
reference: |
  public class Solution
  {
      public Task<List<string>> GetNames(ShopDb db, DateTime since) =>
          db.Customers
            .Where(c => c.Orders.Any(o => o.PlacedAt >= since))
            .Select(c => c.Name)
            .OrderBy(n => n)
            .ToListAsync();
  }
tests:
  - { name: Last day, args: ["2026-05-18T00:00:00Z"], expected: { $ref: reference }, compare: exact }
  - { name: Future date, args: ["2030-01-01T00:00:00Z"], expected: [] }
planRules:
  - { id: one-trip, type: maxRoundTrips, value: 1 }
  - { id: uses-index, type: usesIndex, index: ix_orders_placed_at }
```

Loading every order into memory and filtering in C# fails `one-trip` or the time budget; a per-customer query loop (N+1) fails `one-trip`. Check the plan rule against the reference on your data first: if Postgres prefers a different plan for the seeded volume, adjust the seed data or the date in the test.

### 6. shipping-cost-refactor

```yaml
slug: shipping-cost-refactor
title: Flatten the Shipping Calculator
track: refactoring
difficulty: easy
free: true
grader: refactoring
signature: "public decimal Cost(string zone, decimal weightKg, bool express)"
statement: |
  Keep the behaviour identical, but bring cyclomatic complexity to 6 or less
  and nesting depth to 2 or less.
starter: |
  public class Solution
  {
      public decimal Cost(string zone, decimal weightKg, bool express)
      {
          decimal cost = 0;
          if (zone == "local")
          {
              if (weightKg <= 1) { cost = 5; }
              else { if (weightKg <= 5) { cost = 8; } else { cost = 8 + (weightKg - 5) * 1.5m; } }
          }
          else
          {
              if (zone == "national")
              {
                  if (weightKg <= 1) { cost = 9; } else { cost = 9 + (weightKg - 1) * 2m; }
              }
              else { throw new ArgumentException("Unknown zone", nameof(zone)); }
          }
          if (express) { cost = cost * 1.5m; }
          return cost;
      }
  }
tests:
  - { name: Local light, args: ["local", 0.5, false], expected: 5 }
  - { name: Local medium, args: ["local", 3, false], expected: 8 }
  - { name: Local heavy, args: ["local", 7, false], expected: 11 }
  - { name: National express, args: ["national", 2, true], expected: 16.5 }
  - { name: Unknown zone throws, hidden: true, args: ["mars", 1, false], expectedException: ArgumentException }
metrics:
  - { id: complexity, type: maxCyclomaticComplexity, value: 6 }
  - { id: nesting, type: maxNestingDepth, value: 2 }
```

The starter itself is the reference for behaviour tests, so `expected` values come from running it once; the metrics fail it until refactored.

## Demo users and submissions (development only)

So the admin dashboard has something to show, the seeder can also create:

| User | Role | Seeded activity |
| --- | --- | --- |
| Your bootstrap email (real Clerk sign-in) | admin | none |
| `demo-ana@codebench.test` | learner | Solved problems 1–3, 12 submissions over 14 days |
| `demo-ben@codebench.test` | learner | Solved 1, failed 3 twice (budget exceeded) |
| `demo-chen@codebench.test` | author | Two draft problems |

Demo users have `IsDemo = true` and `ExternalId = "seed|<name>"`; they can't sign in (no Clerk account) and are only for data. Submissions get realistic timestamps, verdicts and per-test timings so dashboard charts render. Never run demo seeding outside Development; `--seed-demo` refuses when `ASPNETCORE_ENVIRONMENT=Production`.

## The seeder

```csharp
// Database/Seeding/ProblemSeeder.cs
internal sealed class ProblemSeeder(ApplicationDbContext db, IProblemBundlePublisher bundles,
    TimeProvider clock, ILogger<ProblemSeeder> logger)
{
    public async Task SeedAsync(string folder, CancellationToken ct)
    {
        var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

        foreach (var file in Directory.EnumerateFiles(folder, "problem.yaml", SearchOption.AllDirectories))
        {
            var def = deserializer.Deserialize<ProblemDefinition>(await File.ReadAllTextAsync(file, ct));

            // Seed only inserts. Once a problem exists, the admin console owns it.
            if (await db.Problems.AnyAsync(p => p.Slug == def.Slug, ct))
            {
                logger.LogInformation("Skipping {Slug}: already exists", def.Slug);
                continue;
            }

            var problem = Problem.FromDefinition(def, clock.GetUtcNow());
            problem.Publish();                      // seeds go straight to Published, Version = 1
            db.Problems.Add(problem);
            await db.SaveChangesAsync(ct);
            await bundles.PublishAsync(problem, ct); // writes {slug}/v1 to the NATS object store
        }
    }
}
```

```csharp
// Program.cs
if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ProblemSeeder>().SeedAsync("/problems", default);
    await scope.ServiceProvider.GetRequiredService<RoleSeeder>().SeedAsync(default);   // roles + permissions
    if (args.Contains("--seed-demo") && app.Environment.IsDevelopment())
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(default);
    return;
}
```

In Compose, mount the folder and run once: add `volumes: [../problems:/problems:ro]` to `api`, then `docker compose run --rm api --seed`. Role seeding is idempotent and runs on every start too.

**Problem bundles in NATS:** create an Object Store bucket `problem-bundles` at start-up. On publish, the API serialises `{ signature, tests (incl. hidden), schema, seedSql, model, planRules, metrics, limits }` to JSON and stores it under `{slug}/v{version}`. The worker reads it with its NATS credentials, caches it in memory by key, and pipes it into the sandbox with the user code. Because keys include the version, an in-flight submission keeps grading against the version it was submitted for.

## Admin API

All admin slices live under `Features/Admin/` and map to a route group:

```csharp
var admin = app.MapGroup("admin").RequireAuthorization().HasPermission(Permissions.AdminAccess)
               .WithTags("Admin");
```

| Area | Slice | Route | Extra permission |
| --- | --- | --- | --- |
| Dashboard | `GetAdminStats` | `GET /admin/stats?days=14` | `stats:read` |
| Problems | `ListProblems` | `GET /admin/problems?status=&track=&q=&page=` | – |
| Problems | `GetProblemForEdit` | `GET /admin/problems/{id}` (incl. hidden tests, reference) | – |
| Problems | `CreateProblem` | `POST /admin/problems` (creates Draft) | `problems:write` |
| Problems | `UpdateProblem` | `PUT /admin/problems/{id}` with `If-Match` version | `problems:write` |
| Problems | `ValidateProblem` | `POST /admin/problems/{id}/validate` | `problems:write` |
| Problems | `PublishProblem` | `POST /admin/problems/{id}/publish` | `problems:publish` |
| Problems | `ArchiveProblem` | `POST /admin/problems/{id}/archive` | `problems:publish` |
| Problems | `DeleteProblem` | `DELETE /admin/problems/{id}` (Drafts only; otherwise archive) | `problems:delete` |
| Tests | `CreateTestCase`, `UpdateTestCase`, `DeleteTestCase` | `/admin/problems/{id}/tests[/{testId}]` | `tests:write` |
| Tests | `ReorderTestCases` | `PUT /admin/problems/{id}/tests/order` | `tests:write` |
| Import/export | `ImportProblemYaml`, `ExportProblemYaml` | `POST /admin/problems/import`, `GET /admin/problems/{id}/export` | `problems:write` |
| Users | `ListUsers`, `ChangeUserRole`, `DisableUser` | `/admin/users...` | `users:manage` |
| Submissions | `ListAllSubmissions`, `GetAnySubmission`, `RegradeSubmission` | `/admin/submissions...` | `submissions:read-all` |
| Audit | `ListAuditLog` | `GET /admin/audit?entity=&actor=` | `stats:read` |

**Validate before publish:** `ValidateProblem` creates a hidden system submission that runs the **reference solution** through the real worker and sandbox against all tests, and returns per-test results plus measured time and allocations. The editor shows them next to each budget, so you can set budgets at roughly 2–3× the reference's median. `PublishProblem` refuses unless the latest validation for the current draft passed.

**Publish flow:**

1. Admin edits a Draft (or a draft copy of a Published problem; edits never change a live version in place).
2. Validate runs the reference solution; all tests must pass within budget.
3. Publish increments `Version`, writes the bundle `{slug}/v{n}` to NATS, sets `Status = Published`, invalidates `problems:*` caches, and records an `AuditLog` row.
4. New submissions use v{n}; older submissions keep showing results against the version they ran.

**Audit:** a small `SaveChangesInterceptor` writes an `AuditLog` row for every change to `Problem`, `TestCase` and `User.Role`, with before/after JSON and the acting user. Admin endpoints never return hidden test data to non-admin routes.

**`GetAdminStats` response** (computed with SQL, cached 60 seconds):

```json
{
  "users": { "total": 4, "newLast7Days": 1 },
  "problems": { "published": 6, "draft": 2, "archived": 0 },
  "submissions": {
    "last24h": 37,
    "perDay": [{ "date": "2026-11-04", "count": 5 }],
    "verdicts": { "Accepted": 21, "WrongAnswer": 9, "BudgetExceeded": 4, "CompileError": 2, "Timeout": 1 },
    "gradingLatencyMs": { "p50": 1800, "p95": 4200 }
  },
  "hardestProblems": [{ "slug": "merge-intervals", "acceptanceRate": 0.31 }],
  "queue": { "pendingJobs": 0 }
}
```

`queue.pendingJobs` comes from the JetStream consumer info for `graders`; everything else from Postgres. Live system metrics stay in Grafana; this dashboard is about content and learners.

## Admin UI in the React app

Put the console inside the existing app at `/admin`, lazy-loaded so learners never download it. The server enforces permissions; the client guard only hides UI.

```tsx
// routes.tsx
const AdminLayout = lazy(() => import('./features/admin/AdminLayout'));

{
  path: '/admin',
  element: <RequirePermission permission="admin:access"><Suspense fallback={<Spinner />}><AdminLayout /></Suspense></RequirePermission>,
  children: [
    { index: true, element: <AdminDashboard /> },
    { path: 'problems', element: <ProblemsTable /> },
    { path: 'problems/new', element: <ProblemEditor mode="create" /> },
    { path: 'problems/:id', element: <ProblemEditor mode="edit" /> },
    { path: 'users', element: <UsersTable /> },
    { path: 'submissions', element: <SubmissionsTable /> },
    { path: 'audit', element: <AuditLog /> },
  ],
}
```

`RequirePermission` reads `GET /me` (which returns the user's role and permissions) and redirects to `/` on 403.

Add these packages: `@tanstack/react-table` (tables), `react-hook-form` + `@hookform/resolvers` + `zod` (forms), `recharts` (dashboard charts), and a Markdown previewer such as `react-markdown`.

| Page | Contents |
| --- | --- |
| Dashboard | Stat cards (users, published problems, submissions in 24 h, p95 grading latency, pending jobs); submissions-per-day line chart; verdict mix bar chart; hardest problems table |
| Problems | Table with search, track/status/difficulty filters, status badge, version, acceptance rate, last edited by; actions: Edit, Duplicate, Archive, Export YAML; "New problem" and "Import YAML" buttons |
| Problem editor | Tabs: **Details** (title, slug, track, difficulty, free, tags, limits) · **Statement** (Markdown with live preview) · **Code** (Monaco: signature, starter, reference) · **Tests** (editable grid: name, hidden toggle, args JSON, expected JSON, compare, budgets; drag to reorder) · **Rules** (plan rules or metrics for database/refactoring) · **Preview** (renders the learner view). Footer: Save draft · Validate · Publish, with validation results inline |
| Users | Table: email, role (dropdown), joined, solved count, last active, disabled toggle; demo users labelled |
| Submissions | Filter by user, problem, verdict, date; open any submission to see code and per-test results; Regrade |
| Audit log | Who changed what, when, with a before/after JSON diff |

Show the Admin link in the app header when `/me` reports `admin:access` (or the Clerk session claim hints at it).

## Tests for the admin side

- [ ] Integration: a learner gets 403 on every `/admin/*` route; an author can edit but not publish; an admin can publish.
- [ ] Integration: `PublishProblem` without a passing validation returns 409.
- [ ] Integration: updating with a stale `If-Match` version returns 409.
- [ ] Integration: `GET /problems/{slug}` never contains hidden tests or the reference solution (assert on the JSON).
- [ ] Integration: publishing writes `{slug}/v{n}` to the NATS object store (Testcontainers NATS).
- [ ] Unit: each `compare` mode and each generator (same seed, same output).
- [ ] E2E: admin creates a problem, adds a test, validates, publishes; a learner solves it.

## Roadmap placement

Add seeding to **Phase 1** (roles, bootstrap admin, problems 1–3 via YAML). Add the data-driven harness and bundles to **Phase 2**. Build the admin console as a new phase between 3 and 4: problems table and editor first, then tests grid and Validate/Publish, then users, submissions, dashboard and audit log. Seed problems 5 and 6 when the database and refactoring graders land in Phases 5 and 6.
