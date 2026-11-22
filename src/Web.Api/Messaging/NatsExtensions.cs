using NATS.Client.Core;

namespace Web.Api.Messaging;

internal static class NatsExtensions
{
    /// <summary>
    /// One shared <see cref="INatsConnection"/> for the process, from <c>ConnectionStrings:Nats</c>
    /// (<c>nats://user:pass@host:4222</c> — credentials travel in the URL). It connects lazily on first use, so a
    /// NATS outage surfaces in <c>/health</c> instead of failing startup. Phase 2 builds JetStream on top of it.
    /// </summary>
    internal static IServiceCollection AddNats(this IServiceCollection services, IConfiguration configuration)
    {
        string? url = configuration.GetConnectionString("Nats");

        services.AddSingleton<INatsConnection>(sp =>
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Nats is not configured (dev: set it with dotnet user-secrets — see docs/guide/08-config-and-secrets.md).");
            }

            return new NatsConnection(NatsOpts.Default with
            {
                Url = url,
                Name = "codebench-api",
                LoggerFactory = sp.GetRequiredService<ILoggerFactory>()
            });
        });

        return services;
    }
}
