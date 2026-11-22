using StackExchange.Redis;

namespace Web.Api.Common.Caching;

/// <summary>The health check's own lazily opened Redis connection (see <see cref="RedisExtensions.AddRedisCache"/>).</summary>
internal sealed class RedisHealthConnection(ConfigurationOptions? options) : IDisposable
{
    private readonly Lazy<IConnectionMultiplexer> _connection = new(() =>
        options is null
            ? throw new InvalidOperationException(
                "ConnectionStrings:Redis is not configured (dev: set it with dotnet user-secrets — see docs/guide/08-config-and-secrets.md).")
            : ConnectionMultiplexer.Connect(options));

    public IConnectionMultiplexer Connection => _connection.Value;

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }
}
