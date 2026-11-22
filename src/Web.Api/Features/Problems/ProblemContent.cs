namespace Web.Api.Features.Problems;

/// <summary>
/// Everything an author edits on a problem. One shape for the YAML seeder (1.5), create and update
/// (3A), so the entity has a single place where content is applied.
/// </summary>
public sealed record ProblemContent
{
    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required Track Track { get; init; }

    public required Difficulty Difficulty { get; init; }

    public required GraderKind GraderKind { get; init; }

    public bool IsFree { get; init; }

    /// <summary>Method the harness calls, e.g. <c>public long SumOfEvens(int[] nums)</c>.</summary>
    public required string Signature { get; init; }

    public required string StatementMarkdown { get; init; }

    public required string StarterCode { get; init; }

    /// <summary>Admin-only. Never projected into a public response (QY-04).</summary>
    public required string ReferenceSolution { get; init; }

    public ProblemLimits Limits { get; init; } = ProblemLimits.Default;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public int SortOrder { get; init; }

    /// <summary>Database grader: DDL run in the sandbox's Postgres before the tests.</summary>
    public string? Schema { get; init; }

    /// <summary>Database grader: data loaded after <see cref="Schema"/>.</summary>
    public string? SeedSql { get; init; }

    /// <summary>Database grader: entity model compiled into the harness, shown read-only to the user.</summary>
    public string? Model { get; init; }

    /// <summary>Database grader: plan rules as a JSON array.</summary>
    public string? PlanRulesJson { get; init; }

    /// <summary>Refactoring grader: metric thresholds as a JSON array.</summary>
    public string? MetricsJson { get; init; }
}
