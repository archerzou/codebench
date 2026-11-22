namespace Web.Api.Features.Problems;

/// <summary>Which sandbox grader runs the problem; selects the sandbox image (Contracts: <c>GradeRequested.GraderKind</c>).</summary>
public enum GraderKind
{
    Algorithms,
    Database,
    Refactoring,
    Architecture,
    SecureCoding,
    TestWriting
}
