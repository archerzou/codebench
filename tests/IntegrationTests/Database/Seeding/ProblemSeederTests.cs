using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Database;
using Web.Api.Database.Seeding;
using Web.Api.Features.Problems;

namespace IntegrationTests.Database.Seeding;

/// <summary>1.5: <c>problems/*.yaml</c> → Postgres, insert-only, all-or-nothing validation (ADR 0005).</summary>
public sealed class ProblemSeederTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly string[] AlgorithmSlugs = ["first-unique-char", "merge-intervals", "sum-of-evens", "top-k-words"];

    private async Task<ProblemSeeder.SeedResult> SeedAsync(string folder)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProblemSeeder>().SeedAsync(folder, CancellationToken.None);
    }

    private T Query<T>(Func<ApplicationDbContext, T> query)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static string ValidYaml(string slug) => $$"""
        slug: {{slug}}
        title: Seeder test {{slug}}
        track: algorithms
        difficulty: easy
        signature: "public int Id(int x)"
        statement: Return x.
        starter: public class Solution { }
        reference: public class Solution { /* ref */ }
        tests:
          - { name: Example, args: [1], expected: 1 }
        """;

    private static string TempFolder(params (string Slug, string Yaml)[] files)
    {
        string folder = Path.Combine(Path.GetTempPath(), $"codebench-seed-{Guid.NewGuid():N}");
        foreach ((string slug, string yaml) in files)
        {
            Directory.CreateDirectory(Path.Combine(folder, slug));
            File.WriteAllText(Path.Combine(folder, slug, "problem.yaml"), yaml);
        }

        return folder;
    }

    private static string UniqueSlug() => $"seed-test-{Guid.NewGuid():N}"[..30];

    [Fact]
    public async Task SeedAsync_Should_PublishAlgorithmProblemsAndDeferTheRest_When_SeedingRepository()
    {
        ProblemSeeder.SeedResult result = await SeedAsync(RepositoryPaths.Problems);

        result.Inserted.Concat(result.Skipped).ShouldBe(AlgorithmSlugs, ignoreOrder: true);
        result.Deferred.ShouldBe(["customers-since", "shipping-cost-refactor"], ignoreOrder: true);

        var seeded = Query(db => db.Problems.AsNoTracking().Where(p => AlgorithmSlugs.Contains(p.Slug))
            .Select(p => new { p.Slug, p.Status, p.Version, p.PublishedAt, p.SortOrder }).ToList());
        seeded.Count.ShouldBe(4);
        seeded.ShouldAllBe(p => p.Status == ProblemStatus.Published && p.Version == 1 && p.PublishedAt != null);
        seeded.Single(p => p.Slug == "sum-of-evens").SortOrder.ShouldBe(10);
        Query(db => db.Problems.Any(p => p.Slug == "customers-since" || p.Slug == "shipping-cost-refactor")).ShouldBeFalse();
    }

    [Fact]
    public async Task SeedAsync_Should_StoreTestsWithJsonTypes_When_Seeded()
    {
        await SeedAsync(RepositoryPaths.Problems);

        Guid sumOfEvens = Query(db => db.Problems.Single(p => p.Slug == "sum-of-evens").Id);
        TestCase[] tests = Query(db => db.TestCases.AsNoTracking().Where(t => t.ProblemId == sumOfEvens).OrderBy(t => t.Order).ToArray());

        tests.Length.ShouldBe(5);
        tests.Count(t => t.IsHidden).ShouldBe(2);
        JsonNode.DeepEquals(JsonNode.Parse(tests[0].ArgsJson), JsonNode.Parse("[[1,2,3,4]]")).ShouldBeTrue();
        JsonNode.DeepEquals(JsonNode.Parse(tests[0].ExpectedJson!), JsonNode.Parse("6")).ShouldBeTrue();
        tests[4].AllocBudgetBytes.ShouldBe(1024);
        Query(db => db.Problems.Single(p => p.Slug == "sum-of-evens").StarterCode).ShouldNotContain("\r");
    }

    [Fact]
    public async Task SeedAsync_Should_SkipEverything_When_RunAgain()
    {
        await SeedAsync(RepositoryPaths.Problems);

        ProblemSeeder.SeedResult second = await SeedAsync(RepositoryPaths.Problems);

        second.Inserted.ShouldBeEmpty();
        second.Skipped.ShouldBe(AlgorithmSlugs, ignoreOrder: true);
        Query(db => db.Problems.Count(p => AlgorithmSlugs.Contains(p.Slug))).ShouldBe(4);
    }

    [Fact]
    public async Task SeedAsync_Should_NeverOverwrite_When_DatabaseContentWasEdited()
    {
        string slug = UniqueSlug();
        string folder = TempFolder((slug, ValidYaml(slug)));
        try
        {
            await SeedAsync(folder);
            using (IServiceScope scope = Factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database
                    .ExecuteSqlAsync($"UPDATE problems SET title = 'Edited in the admin console' WHERE slug = {slug}");
            }

            (await SeedAsync(folder)).Skipped.ShouldBe([slug]);

            Query(db => db.Problems.Single(p => p.Slug == slug).Title).ShouldBe("Edited in the admin console");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task SeedAsync_Should_SeedNothing_When_AnyFileIsInvalid()
    {
        string good = UniqueSlug();
        string folder = TempFolder((good, ValidYaml(good)), ("zz-broken", ValidYaml("zz-broken").Replace("track: algorithms", "track: quantum", StringComparison.Ordinal)));
        try
        {
            ProblemDefinitionException error = await Should.ThrowAsync<ProblemDefinitionException>(() => SeedAsync(folder));

            error.Message.ShouldContain("zz-broken");
            error.Message.ShouldContain("unknown track 'quantum'");
            Query(db => db.Problems.Any(p => p.Slug == good)).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task SeedAsync_Should_Reject_When_TwoFilesShareASlug()
    {
        string slug = UniqueSlug();
        string folder = TempFolder(("a", ValidYaml(slug)), ("b", ValidYaml(slug)));
        try
        {
            (await Should.ThrowAsync<ProblemDefinitionException>(() => SeedAsync(folder))).Message.ShouldContain("more than one");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task SeedAsync_Should_ClearProblemCacheTag_When_ProblemsWereInserted()
    {
        HybridCache cache = Factory.Services.GetRequiredService<HybridCache>();
        string key = $"problems:test:{Guid.NewGuid():N}";
        await cache.SetAsync(key, "stale", tags: [ProblemCacheKeys.Tag]);
        string slug = UniqueSlug();
        string folder = TempFolder((slug, ValidYaml(slug)));
        try
        {
            (await SeedAsync(folder)).Inserted.ShouldBe([slug]);

            string value = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("fresh"));
            value.ShouldBe("fresh");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task SeedAsync_Should_InsertAll_When_SeveralProblemsUseDefaultLimits()
    {
        // ValidYaml has no limits: — every problem gets ProblemLimits.Default, an EF owned type tracked by identity.
        string first = UniqueSlug(), second = UniqueSlug();
        string folder = TempFolder((first, ValidYaml(first)), (second, ValidYaml(second)));
        try
        {
            (await SeedAsync(folder)).Inserted.ShouldBe([first, second], ignoreOrder: true);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task SeedAsync_Should_Throw_When_FolderIsMissing()
    {
        await Should.ThrowAsync<DirectoryNotFoundException>(() => SeedAsync(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}")));
    }
}
