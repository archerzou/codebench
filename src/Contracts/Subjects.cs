namespace CodeBench.Contracts;

/// <summary>
/// NATS JetStream subjects. Stream <c>JOBS</c> captures <c>jobs.&gt;</c> (consumed by workers),
/// stream <c>RESULTS</c> captures <c>results.&gt;</c> (consumed by the API).
/// </summary>
public static class Subjects
{
    public const string RunsCreated = "jobs.runs.created";
    public const string SubmissionsCreated = "jobs.submissions.created";
    public const string Graded = "results.graded";
}
