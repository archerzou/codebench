using System.Text.Json;

namespace CodeBench.Contracts;

/// <summary>
/// Published by the API (via the outbox) when a run or submission needs grading.
/// Carries the problem slug + version, never the hidden tests: the worker fetches the
/// versioned problem bundle (<c>{slug}/v{version}</c>) from the NATS object store.
/// </summary>
public sealed record GradeRequested(
    Guid SubmissionId,
    string ProblemSlug,
    int ProblemVersion,
    string GraderKind,
    bool IncludeHidden,
    string Code,
    int TimeoutSeconds,
    int MemoryMb);

public sealed record TestOutcome(
    string Name,
    bool Passed,
    long ElapsedMicros,
    long AllocatedBytes,
    string? Message,
    JsonElement? Details);

/// <summary>Published by the worker on <see cref="Subjects.Graded"/>.</summary>
public sealed record GradeCompleted(
    Guid SubmissionId,
    string Verdict,
    string? CompilerOutput,
    IReadOnlyList<TestOutcome> Tests,
    DateTimeOffset CompletedAt);

/// <summary>Verdict values carried by <see cref="GradeCompleted.Verdict"/>.</summary>
public static class Verdicts
{
    public const string Accepted = "Accepted";
    public const string WrongAnswer = "WrongAnswer";
    public const string CompileError = "CompileError";
    public const string Timeout = "Timeout";
    public const string BudgetExceeded = "BudgetExceeded";
    public const string InternalError = "InternalError";
}
