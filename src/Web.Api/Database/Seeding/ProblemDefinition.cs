using System.Text.Json;
using Web.Api.Features.Problems;

namespace Web.Api.Database.Seeding;

/// <summary>
/// The shape of <c>problems/&lt;slug&gt;/problem.yaml</c> (guide 09). Read by <see cref="ProblemDefinitionReader"/>;
/// unknown keys are rejected so a typo can't silently drop a budget.
/// </summary>
internal sealed record ProblemDefinition
{
    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string Track { get; init; }

    public required string Difficulty { get; init; }

    /// <summary>Defaults to the track (<c>algorithms</c> → <c>algorithms</c>).</summary>
    public string? Grader { get; init; }

    public bool Free { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public int SortOrder { get; init; }

    public required string Signature { get; init; }

    public required string Statement { get; init; }

    public required string Starter { get; init; }

    /// <summary>Required, except for the refactoring grader, where the starter is the behaviour reference.</summary>
    public string? Reference { get; init; }

    public LimitsDefinition? Limits { get; init; }

    public string? Schema { get; init; }

    public string? SeedSql { get; init; }

    public string? Model { get; init; }

    public JsonElement? PlanRules { get; init; }

    public JsonElement? Metrics { get; init; }

    public required IReadOnlyList<TestDefinition> Tests { get; init; }

    public GraderKind GraderKind => ProblemEnums.Parse<GraderKind>(Grader ?? Track);

    public ProblemContent ToContent() => new()
    {
        Slug = Slug,
        Title = Title,
        Track = ProblemEnums.Parse<Track>(Track),
        Difficulty = ProblemEnums.Parse<Difficulty>(Difficulty),
        GraderKind = GraderKind,
        IsFree = Free,
        Signature = Signature,
        StatementMarkdown = Statement,
        StarterCode = Starter,
        ReferenceSolution = Reference ?? Starter,
        Limits = Limits is null ? ProblemLimits.Default : new ProblemLimits(Limits.TimeoutSeconds, Limits.MemoryMb),
        Tags = Tags,
        SortOrder = SortOrder,
        Schema = Schema,
        SeedSql = SeedSql,
        Model = Model,
        PlanRulesJson = PlanRules?.GetRawText(),
        MetricsJson = Metrics?.GetRawText()
    };

    public IEnumerable<TestCase> ToTestCases(Guid problemId) =>
        Tests.Select((test, order) => TestCase.Create(problemId, order, test.ToContent()));
}
