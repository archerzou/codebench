namespace CodeBench.Contracts;

/// <summary>Published by the worker on <see cref="Subjects.Graded"/>.</summary>
public sealed record GradeCompleted(
    Guid SubmissionId,
    string Verdict,
    string? CompilerOutput,
    IReadOnlyList<TestOutcome> Tests,
    DateTimeOffset CompletedAt);
