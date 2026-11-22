using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Web.Api.Common.Caching;

internal static class RedisExtensions
{
    /// <summary>
    /// Redis as the <c>HybridCache</c> L2 (guide 02 §8). Must run <b>before</b> <c>AddHybridCache()</c> so HybridCache
    /// picks up the Redis <c>IDistributedCache</c>. <c>AbortOnConnectFail = false</c>: a Redis outage degrades to
    /// L1-only and shows in <c>/health</c> rather than crashing startup.
    /// <para>
    /// <b>Why not <c>AddStackExchangeRedisCache()</c>:</b> it registers <c>RedisCacheImpl</c>, which on connect calls
    /// <c>IServiceProvider.GetService&lt;HybridCache&gt;()</c> (only to tag the Redis client name). HybridCache's
    /// constructor reads tag state from L2, i.e. connects, so that call re-enters DI while the first
    /// <c>HybridCache</c> is still being built and creates a <b>second singleton</b>. Consumers then disagree on the
    /// instance (method-injected middleware vs constructor-injected handlers), and a tag/key invalidation in one never
    /// reaches the other's L1 → stale data for up to 10 minutes. The public <see cref="RedisCache"/> gets no service
    /// provider, so it cannot re-enter. Pinned by <c>CachingTests</c>.
    /// </para>
    /// </summary>
    internal static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Redis");
        ConfigurationOptions? redisOptions = null;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            redisOptions = ConfigurationOptions.Parse(connectionString);
            redisOptions.AbortOnConnectFail = false;
        }

        // Unconfigured: no L2 at all (HybridCache runs L1-only) and /health reports Redis Unhealthy with the reason —
        // better than pointing L2 at a dead host and paying a connect timeout on every cache call.
        if (redisOptions is not null)
        {
            services.Configure<RedisCacheOptions>(options => options.ConfigurationOptions = redisOptions);
            services.AddSingleton<IDistributedCache>(sp => new RedisCache(sp.GetRequiredService<IOptions<RedisCacheOptions>>()));
        }

        services.AddSingleton(_ => new RedisHealthConnection(redisOptions));

        return services;
    }
}
