using System.Text.Json;

namespace CodeBench.Contracts;

/// <summary>One test's result inside <see cref="GradeCompleted"/>.</summary>
public sealed record TestOutcome(
    string Name,
    bool Passed,
    long ElapsedMicros,
    long AllocatedBytes,
    string? Message,
    JsonElement? Details);
