namespace Web.Api.Database.Seeding;

/// <summary>A seed file is unreadable or breaks a content rule. The message always names the file.</summary>
public sealed class ProblemDefinitionException : Exception
{
    public ProblemDefinitionException()
    {
    }

    public ProblemDefinitionException(string message)
        : base(message)
    {
    }

    public ProblemDefinitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static ProblemDefinitionException For(string file, string problem, Exception? innerException = null) =>
        innerException is null
            ? new ProblemDefinitionException($"{file}: {problem}")
            : new ProblemDefinitionException($"{file}: {problem}", innerException);
}
