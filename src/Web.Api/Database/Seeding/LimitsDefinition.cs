namespace Web.Api.Database.Seeding;

/// <summary><c>limits: { timeoutSeconds, memoryMb }</c> in <c>problem.yaml</c>.</summary>
internal sealed record LimitsDefinition(int TimeoutSeconds, int MemoryMb);
