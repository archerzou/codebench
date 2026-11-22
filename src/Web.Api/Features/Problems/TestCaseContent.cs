namespace Web.Api.Features.Problems;

/// <summary>
/// A data-driven test (guide 09): JSON arguments, an expected JSON value <b>or</b> an expected exception
/// type, a comparison mode and optional budgets. Generators live inside <see cref="ArgsJson"/> as
/// <c>{"$gen": …}</c>; <c>{"$ref": "reference"}</c> in <see cref="ExpectedJson"/> means "run the reference".
/// </summary>
public sealed record TestCaseContent
{
    public required string Name { get; init; }

    public bool IsHidden { get; init; }

    /// <summary>JSON array, one element per method parameter.</summary>
    public required string ArgsJson { get; init; }

    public string? ExpectedJson { get; init; }

    /// <summary>Exception type name the call must throw, e.g. <c>ArgumentException</c>.</summary>
    public string? ExpectedException { get; init; }

    public CompareMode Compare { get; init; } = CompareMode.Exact;

    public int? TimeBudgetMs { get; init; }

    public long? AllocBudgetBytes { get; init; }
}
