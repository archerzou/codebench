using System.Text.Json;
using Web.Api.Features.Problems;

namespace Web.Api.Database.Seeding;

/// <summary>One entry of <c>tests:</c> in <c>problem.yaml</c> — the data-driven format of guide 09.</summary>
internal sealed record TestDefinition
{
    public required string Name { get; init; }

    public bool Hidden { get; init; }

    /// <summary>JSON array, one element per method parameter; may contain <c>{"$gen": …}</c> generators.</summary>
    public required JsonElement Args { get; init; }

    /// <summary>Expected return value, or <c>{"$ref": "reference"}</c>.</summary>
    public JsonElement? Expected { get; init; }

    public string? ExpectedException { get; init; }

    public string? Compare { get; init; }

    public int? TimeBudgetMs { get; init; }

    public long? AllocBudgetBytes { get; init; }

    public TestCaseContent ToContent() => new()
    {
        Name = Name,
        IsHidden = Hidden,
        ArgsJson = Args.GetRawText(),
        ExpectedJson = Expected?.GetRawText(),
        ExpectedException = ExpectedException,
        Compare = Compare is null ? CompareMode.Exact : ProblemEnums.Parse<CompareMode>(Compare),
        TimeBudgetMs = TimeBudgetMs,
        AllocBudgetBytes = AllocBudgetBytes
    };
}
