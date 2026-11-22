using Web.Api.Database.Seeding;
using Web.Api.UnitTests.Abstractions;

namespace Web.Api.UnitTests.Seeding;

public sealed class ProblemDefinitionReaderTests
{
    private const string File = "problems/test/problem.yaml";

    private const string Valid = """
        slug: sum-of-evens
        title: Sum of Evens
        track: algorithms
        difficulty: easy
        free: true
        sortOrder: 10
        tags: [arrays]
        signature: "public long SumOfEvens(int[] nums)"
        statement: Return the sum.
        starter: public class Solution { }
        reference: public class Solution { /* ref */ }
        tests:
          - { name: Example, args: [[1, 2, 3, 4]], expected: 6 }
          - { name: All odd, hidden: true, args: [[1, 3]], expected: 0 }
        """;

    public static TheoryData<string> SeedFiles() =>
        [.. Directory.EnumerateFiles(RepositoryPaths.Problems, "problem.yaml", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(RepositoryPaths.Root, f))];

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void Read_Should_AcceptEveryRepositorySeedFile_When_Committed(string relativePath)
    {
        string path = Path.Combine(RepositoryPaths.Root, relativePath);

        ProblemDefinition definition = ProblemDefinitionReader.Read(System.IO.File.ReadAllText(path), path);

        definition.Slug.ShouldBe(Path.GetFileName(Path.GetDirectoryName(path)));   // folder name = slug
    }

    [Fact]
    public void SeedFiles_Should_CoverTheSixGuideProblems_When_Listed()
    {
        SeedFiles().Count.ShouldBe(6);
    }

    [Fact]
    public void Read_Should_ReturnDefinition_When_Valid()
    {
        ProblemDefinition definition = ProblemDefinitionReader.Read(Valid, File);

        definition.Slug.ShouldBe("sum-of-evens");
        definition.Tests.Count.ShouldBe(2);
        definition.Tests[0].Args.GetRawText().ShouldBe("[[1,2,3,4]]");
        definition.Tests[0].Expected!.Value.GetRawText().ShouldBe("6");
    }

    [Theory]
    [InlineData("slug: Sum_Of_Evens", "kebab-case")]
    [InlineData("track: quantum", "unknown track 'quantum'")]
    [InlineData("difficulty: 2", "'$.difficulty'")]              // a plain 2 is a number: rejected by shape
    [InlineData("difficulty: \"2\"", "unknown difficulty '2'")]  // quoted: rejected by the enum rule
    [InlineData("grader: magic", "unknown grader 'magic'")]
    [InlineData("tags: [Arrays]", "tags are lowercase kebab-case")]
    [InlineData("reference: ''", "reference is required")]
    [InlineData("limits: { timeoutSeconds: 0, memoryMb: 256 }", "Timeout Seconds")]
    [InlineData("planRules: { id: x }", "planRules must be a list")]
    public void Read_Should_RejectWithReason_When_FieldIsInvalid(string replacementLine, string expectedReason)
    {
        string key = replacementLine[..replacementLine.IndexOf(':', StringComparison.Ordinal)];
        string yaml = Valid.Contains(key + ":", StringComparison.Ordinal)
            ? string.Join('\n', Valid.Split('\n').Select(l => l.StartsWith(key + ":", StringComparison.Ordinal) ? replacementLine : l))
            : Valid + "\n" + replacementLine;

        ProblemDefinitionException error = Should.Throw<ProblemDefinitionException>(() => ProblemDefinitionReader.Read(yaml, File));

        error.Message.ShouldStartWith(File);
        error.Message.ShouldContain(expectedReason, Case.Insensitive);
    }

    [Theory]
    [InlineData("  - { name: Bad, args: [1], expected: 1, expectedException: ArgumentException }", "exactly one of expected / expectedException")]
    [InlineData("  - { name: Bad, args: [1] }", "exactly one of expected / expectedException")]
    [InlineData("  - { name: Bad, args: 1, expected: 1 }", "args must be a list")]
    [InlineData("  - { name: Bad, args: [1], expected: 1, compare: fuzzy }", "unknown compare 'fuzzy'")]
    [InlineData("  - { name: Bad, args: [1], expected: 1, timeBudgetMs: 0 }", "Time Budget Ms")]
    [InlineData("  - { name: example, args: [1], expected: 1 }", "test names must be unique")]
    public void Read_Should_RejectWithReason_When_TestIsInvalid(string extraTest, string expectedReason)
    {
        ProblemDefinitionException error = Should.Throw<ProblemDefinitionException>(() => ProblemDefinitionReader.Read(Valid + "\n" + extraTest, File));

        error.Message.ShouldContain(expectedReason, Case.Insensitive);
    }

    [Fact]
    public void Read_Should_Reject_When_EveryTestIsHidden()
    {
        string yaml = Valid.Replace("{ name: Example, args", "{ name: Example, hidden: true, args", StringComparison.Ordinal);

        Should.Throw<ProblemDefinitionException>(() => ProblemDefinitionReader.Read(yaml, File))
            .Message.ShouldContain("at least one visible test");
    }

    [Fact]
    public void Read_Should_Reject_When_KeyIsUnknown()
    {
        ProblemDefinitionException error = Should.Throw<ProblemDefinitionException>(() =>
            ProblemDefinitionReader.Read(Valid.Replace("sortOrder: 10", "sortorder: 10", StringComparison.Ordinal), File));

        error.Message.ShouldContain("sortorder");
    }

    [Fact]
    public void Read_Should_Reject_When_RequiredKeyIsMissing()
    {
        string yaml = string.Join('\n', Valid.Split('\n').Where(l => !l.StartsWith("signature:", StringComparison.Ordinal)));

        Should.Throw<ProblemDefinitionException>(() => ProblemDefinitionReader.Read(yaml, File)).Message.ShouldContain("signature");
    }

    [Fact]
    public void Read_Should_ReportLine_When_YamlIsMalformed()
    {
        ProblemDefinitionException error = Should.Throw<ProblemDefinitionException>(() => ProblemDefinitionReader.Read("slug: a\ntests: [ { name: x", File));

        error.Message.ShouldStartWith(File);
        error.Message.ShouldContain("invalid YAML at line");
    }

    [Fact]
    public void Read_Should_AllowMissingReference_When_GraderIsRefactoring()
    {
        string yaml = string.Join('\n', Valid.Split('\n').Where(l => !l.StartsWith("reference:", StringComparison.Ordinal)))
            .Replace("track: algorithms", "track: refactoring", StringComparison.Ordinal);

        ProblemDefinitionReader.Read(yaml, File).Reference.ShouldBeNull();
    }
}
