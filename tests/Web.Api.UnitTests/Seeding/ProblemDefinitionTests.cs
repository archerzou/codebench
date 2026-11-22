using Web.Api.Database.Seeding;
using Web.Api.Features.Problems;
using Web.Api.UnitTests.Abstractions;

namespace Web.Api.UnitTests.Seeding;

public sealed class ProblemDefinitionTests
{
    private static ProblemDefinition Load(string slug)
    {
        string path = Path.Combine(RepositoryPaths.Problems, slug, "problem.yaml");
        return ProblemDefinitionReader.Read(File.ReadAllText(path), path);
    }

    [Fact]
    public void ToContent_Should_MapEveryField_When_AlgorithmsProblem()
    {
        ProblemContent content = Load("sum-of-evens").ToContent();

        content.Slug.ShouldBe("sum-of-evens");
        content.Track.ShouldBe(Track.Algorithms);
        content.Difficulty.ShouldBe(Difficulty.Easy);
        content.GraderKind.ShouldBe(GraderKind.Algorithms);
        content.IsFree.ShouldBeTrue();
        content.SortOrder.ShouldBe(10);
        content.Signature.ShouldBe("public long SumOfEvens(int[] nums)");
        content.Limits.ShouldBe(new ProblemLimits(10, 256));
        content.Tags.ShouldBe(["arrays", "allocations"]);
        content.ReferenceSolution.ShouldContain("sum += n");
        content.PlanRulesJson.ShouldBeNull();
    }

    [Fact]
    public void ToTestCases_Should_KeepJsonTypesAndOrder_When_Mapped()
    {
        var problemId = Guid.NewGuid();

        TestCase[] tests = [.. Load("sum-of-evens").ToTestCases(problemId)];

        tests.Select(t => t.Order).ShouldBe([0, 1, 2, 3, 4]);
        tests.ShouldAllBe(t => t.ProblemId == problemId);
        tests[0].ArgsJson.ShouldBe("[[1,2,3,4]]");
        tests[0].ExpectedJson.ShouldBe("6");
        tests[0].IsHidden.ShouldBeFalse();
        tests[0].Compare.ShouldBe(CompareMode.Exact);
        tests[4].IsHidden.ShouldBeTrue();
        tests[4].ArgsJson.ShouldBe("""[{"$gen":"randomInts","count":200000,"min":-1000,"max":1000,"seed":42}]""");
        tests[4].ExpectedJson.ShouldBe("""{"$ref":"reference"}""");
        tests[4].TimeBudgetMs.ShouldBe(30);
        tests[4].AllocBudgetBytes.ShouldBe(1024);
    }

    [Fact]
    public void ToTestCases_Should_KeepQuotedStringsAsStrings_When_ArgsAreText()
    {
        TestCase first = Load("first-unique-char").ToTestCases(Guid.NewGuid()).First();

        first.ArgsJson.ShouldBe("""["banana"]""");
        first.ExpectedJson.ShouldBe("0");
    }

    [Fact]
    public void ToTestCases_Should_ParseCompareMode_When_Given()
    {
        Load("merge-intervals").ToTestCases(Guid.NewGuid()).First().Compare.ShouldBe(CompareMode.UnorderedNested);
    }

    [Fact]
    public void ToContent_Should_UseStarterAsReference_When_RefactoringProblemHasNone()
    {
        ProblemDefinition definition = Load("shipping-cost-refactor");

        ProblemContent content = definition.ToContent();

        content.GraderKind.ShouldBe(GraderKind.Refactoring);
        content.ReferenceSolution.ShouldBe(content.StarterCode);
        content.MetricsJson.ShouldBe("""[{"id":"complexity","type":"maxCyclomaticComplexity","value":6},{"id":"nesting","type":"maxNestingDepth","value":2}]""");
        definition.ToTestCases(Guid.NewGuid()).Last().ExpectedException.ShouldBe("ArgumentException");
    }

    [Fact]
    public void ToContent_Should_CarryDatabaseFields_When_DatabaseProblem()
    {
        ProblemContent content = Load("customers-since").ToContent();

        content.GraderKind.ShouldBe(GraderKind.Database);
        content.Schema.ShouldNotBeNull().ShouldContain("CREATE INDEX ix_orders_placed_at");
        content.SeedSql.ShouldNotBeNull().ShouldContain("generate_series");
        content.Model.ShouldNotBeNull().ShouldContain("class Customer");
        content.PlanRulesJson.ShouldNotBeNull().ShouldContain("\"usesIndex\"");
        content.Limits.ShouldBe(new ProblemLimits(20, 512));
    }
}
