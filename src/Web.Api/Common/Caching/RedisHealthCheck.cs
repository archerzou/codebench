using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Web.Api.Common.Caching;

/// <summary>PINGs Redis; a missing connection string is reported as Unhealthy, not thrown.</summary>
internal sealed class RedisHealthCheck(RedisHealthConnection redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            TimeSpan rtt = await redis.Connection.GetDatabase().PingAsync().WaitAsync(cancellationToken);

            return HealthCheckResult.Healthy($"PING round trip {rtt.TotalMilliseconds:F0} ms");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Redis is unreachable", exception);
        }
    }
}
