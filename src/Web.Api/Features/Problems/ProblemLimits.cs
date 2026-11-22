namespace Web.Api.Features.Problems;

/// <summary>Sandbox limits for one run of a problem (stored as jsonb; travels in the bundle).</summary>
public sealed record ProblemLimits(int TimeoutSeconds, int MemoryMb)
{
    /// <summary>
    /// A <b>new</b> instance per access: <c>Limits</c> is an EF owned type, tracked by object identity, so two
    /// problems must never share one instance (a shared static made saving two default-limit problems throw).
    /// </summary>
    public static ProblemLimits Default => new(TimeoutSeconds: 10, MemoryMb: 256);
}
