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
