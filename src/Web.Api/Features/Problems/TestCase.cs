using Web.Api.Common;

namespace Web.Api.Features.Problems;

/// <summary>
/// One test of a <see cref="Problem"/> (FK only, no navigation — DM-03). Hidden tests never leave the
/// server on public routes (QY-04); they reach the sandbox only through the versioned bundle.
/// </summary>
public sealed class TestCase : Entity
{
    private TestCase()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProblemId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Position within the problem; visible tests are shown in this order.</summary>
    public int Order { get; private set; }

    public bool IsHidden { get; private set; }

    public string ArgsJson { get; private set; } = "[]";

    public string? ExpectedJson { get; private set; }

    public string? ExpectedException { get; private set; }

    public CompareMode Compare { get; private set; }

    public int? TimeBudgetMs { get; private set; }

    public long? AllocBudgetBytes { get; private set; }

    public static TestCase Create(Guid problemId, int order, TestCaseContent content)
    {
        var testCase = new TestCase
        {
            Id = Guid.CreateVersion7(),
            ProblemId = problemId,
            Order = order
        };
        testCase.Apply(content);

        return testCase;
    }

    public void MoveTo(int order)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        Order = order;
    }

    private void Apply(TestCaseContent content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(content.ArgsJson);
        ArgumentOutOfRangeException.ThrowIfNegative(Order);

        // Exactly one expectation; the database enforces the same rule (ck_test_cases_one_expectation).
        if (string.IsNullOrWhiteSpace(content.ExpectedJson) == string.IsNullOrWhiteSpace(content.ExpectedException))
        {
            throw new ArgumentException(
                $"Test '{content.Name}' needs exactly one of ExpectedJson or ExpectedException.", nameof(content));
        }

        if (content.TimeBudgetMs is <= 0 || content.AllocBudgetBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(content), $"Test '{content.Name}' has a negative or zero budget.");
        }

        Name = content.Name;
        IsHidden = content.IsHidden;
        ArgsJson = content.ArgsJson;
        ExpectedJson = string.IsNullOrWhiteSpace(content.ExpectedJson) ? null : content.ExpectedJson;
        ExpectedException = string.IsNullOrWhiteSpace(content.ExpectedException) ? null : content.ExpectedException;
        Compare = content.Compare;
        TimeBudgetMs = content.TimeBudgetMs;
        AllocBudgetBytes = content.AllocBudgetBytes;
    }
}
