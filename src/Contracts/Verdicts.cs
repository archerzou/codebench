namespace CodeBench.Contracts;

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
