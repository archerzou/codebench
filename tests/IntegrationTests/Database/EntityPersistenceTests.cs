using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Web.Api.Database;
using Web.Api.Features.Problems;
using Web.Api.Features.Users;

namespace IntegrationTests.Database;

/// <summary>
/// The 1.2 schema (problems, test cases, users) against real Postgres: jsonb/text[] mapping, constraints, cascade, and the xmin
/// concurrency token (DM-03, DM-07). Unit tests can't see any of this — EF InMemory ignores it.
/// </summary>
public sealed class EntityPersistenceTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly DateTime Now = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);

    private static ProblemContent Content(string slug) => new()
    {
        Slug = slug,
        Title = "Customers Who Ordered Recently",
        Track = Track.Database,
        Difficulty = Difficulty.Medium,
        GraderKind = GraderKind.Database,
        Signature = "public Task<List<string>> GetNames(ShopDb db, DateTime since)",
        StatementMarkdown = "Return the distinct names…",
        StarterCode = "public class Solution { }",
        ReferenceSolution = "public class Solution { /* reference */ }",
        Limits = new ProblemLimits(TimeoutSeconds: 20, MemoryMb: 512),
        Tags = ["ef-core", "indexes"],
        SortOrder = 5,
        Schema = "CREATE TABLE customers (id int PRIMARY KEY);",
        PlanRulesJson = """[{"id":"one-trip","type":"maxRoundTrips","value":1}]"""
    };

    private static string UniqueSlug() => $"problem-{Guid.NewGuid():N}"[..40];

    private ApplicationDbContext NewDbContext(out IServiceScope scope)
    {
        scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    [Fact]
    public async Task Problem_Should_RoundTripEveryColumn_When_Saved()
    {
        var problem = Problem.CreateDraft(Content(UniqueSlug()), Now);
        problem.Publish(Now);
        ApplicationDbContext write = NewDbContext(out IServiceScope writeScope);
        using (writeScope)
        {
            write.Problems.Add(problem);
            write.TestCases.Add(TestCase.Create(problem.Id, 0, new TestCaseContent
            {
                Name = "Future date",
                IsHidden = true,
                ArgsJson = """["2030-01-01T00:00:00Z"]""",
                ExpectedJson = "[]",
                Compare = CompareMode.UnorderedNested,
                TimeBudgetMs = 50
            }));
            await write.SaveChangesAsync();
        }

        ApplicationDbContext read = NewDbContext(out IServiceScope readScope);
        using (readScope)
        {
            Problem loaded = await read.Problems.AsNoTracking().SingleAsync(p => p.Id == problem.Id);
            TestCase test = await read.TestCases.AsNoTracking().SingleAsync(t => t.ProblemId == problem.Id);

            loaded.Status.ShouldBe(ProblemStatus.Published);
            loaded.Version.ShouldBe(1);
            loaded.Track.ShouldBe(Track.Database);
            loaded.Limits.ShouldBe(new ProblemLimits(20, 512));
            loaded.Tags.ShouldBe(["ef-core", "indexes"]);
            loaded.PlanRulesJson.ShouldNotBeNull().ShouldContain("maxRoundTrips");
            test.Compare.ShouldBe(CompareMode.UnorderedNested);
            test.ExpectedJson.ShouldBe("[]");

            // Enums are stored as readable text, not ordinals.
            string status = await read.Database.SqlQuery<string>($"SELECT status AS \"Value\" FROM problems WHERE id = {problem.Id}").SingleAsync();
            status.ShouldBe("Published");
        }
    }

    [Fact]
    public async Task Problem_Should_RejectDuplicateSlug_When_Inserted()
    {
        string slug = UniqueSlug();
        ApplicationDbContext db = NewDbContext(out IServiceScope scope);
        using (scope)
        {
            db.Problems.Add(Problem.CreateDraft(Content(slug), Now));
            await db.SaveChangesAsync();

            db.Problems.Add(Problem.CreateDraft(Content(slug), Now));
            DbUpdateException error = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

            error.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        }
    }

    [Fact]
    public async Task Problem_Should_ThrowConcurrencyConflict_When_SavedWithStaleRowVersion()
    {
        var problem = Problem.CreateDraft(Content(UniqueSlug()), Now);
        ApplicationDbContext seed = NewDbContext(out IServiceScope seedScope);
        using (seedScope)
        {
            seed.Problems.Add(problem);
            await seed.SaveChangesAsync();
        }

        ApplicationDbContext first = NewDbContext(out IServiceScope firstScope);
        ApplicationDbContext second = NewDbContext(out IServiceScope secondScope);
        using (firstScope)
        using (secondScope)
        {
            Problem a = await first.Problems.SingleAsync(p => p.Id == problem.Id);
            Problem b = await second.Problems.SingleAsync(p => p.Id == problem.Id);

            a.Publish(Now);
            await first.SaveChangesAsync();          // xmin changes

            b.Publish(Now.AddMinutes(1));
            await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task TestCase_Should_ViolateCheckConstraint_When_BothExpectationsBypassTheEntity()
    {
        var problem = Problem.CreateDraft(Content(UniqueSlug()), Now);
        ApplicationDbContext db = NewDbContext(out IServiceScope scope);
        using (scope)
        {
            db.Problems.Add(problem);
            await db.SaveChangesAsync();

            PostgresException error = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
                INSERT INTO test_cases (id, problem_id, name, "order", is_hidden, args_json, expected_json, expected_exception, compare)
                VALUES ({Guid.NewGuid()}, {problem.Id}, 'both', 0, false, '[]'::jsonb, '1'::jsonb, 'ArgumentException', 'Exact')
                """));

            error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
            error.ConstraintName.ShouldBe("ck_test_cases_one_expectation");
        }
    }

    [Fact]
    public async Task Problem_Should_CascadeDeleteTests_When_Deleted()
    {
        var problem = Problem.CreateDraft(Content(UniqueSlug()), Now);
        ApplicationDbContext db = NewDbContext(out IServiceScope scope);
        using (scope)
        {
            db.Problems.Add(problem);
            db.TestCases.Add(TestCase.Create(problem.Id, 0, new TestCaseContent { Name = "One", ArgsJson = "[1]", ExpectedJson = "1" }));
            await db.SaveChangesAsync();

            await db.Problems.Where(p => p.Id == problem.Id).ExecuteDeleteAsync();

            (await db.TestCases.CountAsync(t => t.ProblemId == problem.Id)).ShouldBe(0);
        }
    }

    [Fact]
    public async Task User_Should_DefaultToLearnerOnFreePlan_When_InsertedWithoutRole()
    {
        string externalId = UniqueExternalId();
        ApplicationDbContext db = NewDbContext(out IServiceScope scope);
        using (scope)
        {
            // Rows written outside EF (e.g. a future SQL backfill) still get safe defaults.
            await db.Database.ExecuteSqlAsync($"INSERT INTO users (id, external_id, email, created_at) VALUES ({Guid.NewGuid()}, {externalId}, {UniqueEmail()}, {Now})");

            User user = await db.Users.AsNoTracking().SingleAsync(u => u.ExternalId == externalId);

            user.Role.ShouldBe(Role.Learner);
            user.Plan.ShouldBe(Plan.Free);
            user.IsDisabled.ShouldBeFalse();
            user.IsDemo.ShouldBeFalse();
        }
    }
}
