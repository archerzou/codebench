using Microsoft.Extensions.Diagnostics.HealthChecks;
using NATS.Client.Core;

namespace Web.Api.Messaging;

/// <summary>
/// Round-trips a PING to the NATS server. Written here rather than taken from <c>AspNetCore.HealthChecks.Nats</c>,
/// which still depends on NATS.Net 2.x. The registration's timeout bounds the call.
/// </summary>
internal sealed class NatsHealthCheck(IServiceProvider serviceProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            INatsConnection connection = serviceProvider.GetRequiredService<INatsConnection>();
            TimeSpan rtt = await connection.PingAsync(cancellationToken);

            return HealthCheckResult.Healthy($"PING round trip {rtt.TotalMilliseconds:F0} ms");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "NATS is unreachable", exception);
        }
    }
}
