using Web.Api.Database;
using Web.Api.Features.Problems;

namespace Web.Api.UnitTests.Problems;

/// <summary>Builds problems for handler tests (in-memory database).</summary>
internal static class ProblemTestData
{
    public static readonly DateTime Now = new(2026, 11, 21, 8, 0, 0, DateTimeKind.Utc);

    public static ProblemContent Content(
        string slug,
        Track track = Track.Algorithms,
        Difficulty difficulty = Difficulty.Easy,
        bool isFree = true,
        int sortOrder = 0,
        string? title = null,
        IReadOnlyList<string>? tags = null) => new()
    {
        Slug = slug,
        Title = title ?? $"Title {slug}",
        Track = track,
        Difficulty = difficulty,
        GraderKind = GraderKind.Algorithms,
        IsFree = isFree,
        SortOrder = sortOrder,
        Signature = "public int Solve(int x)",
        StatementMarkdown = $"Statement of {slug}",
        StarterCode = "public class Solution { }",
        ReferenceSolution = "public class Solution { /* SECRET-REFERENCE */ }",
        Tags = tags ?? ["arrays"]
    };

    public static async Task<Problem> AddAsync(
        ApplicationDbContext db,
        ProblemContent content,
        ProblemStatus status = ProblemStatus.Published,
        params TestCaseContent[] tests)
    {
        var problem = Problem.CreateDraft(content, Now);
        if (status != ProblemStatus.Draft)
        {
            problem.Publish(Now);
        }

        if (status == ProblemStatus.Archived)
        {
            problem.Archive();
        }

        db.Problems.Add(problem);
        db.TestCases.AddRange(tests.Select((t, i) => TestCase.Create(problem.Id, i, t)));
        await db.SaveChangesAsync();

        return problem;
    }

    public static TestCaseContent Test(string name, bool hidden = false, string args = "[1]", string expected = "1") =>
        new() { Name = name, IsHidden = hidden, ArgsJson = args, ExpectedJson = expected };
}
