using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Database.Seeding;

namespace IntegrationTests.Infrastructure;

/// <summary>1.7.1: Redis is the HybridCache L2, and the whole process shares one HybridCache.</summary>
public sealed class CachingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public void DistributedCache_Should_BeRedis()
    {
        Factory.Services.GetRequiredService<IDistributedCache>().ShouldBeAssignableTo<RedisCache>();
    }

    /// <summary>
    /// Regression: setting <c>RedisCacheOptions.ConnectionMultiplexerFactory</c> made DI hand out two HybridCache
    /// singletons, so the seeder's tag invalidation never reached the L1 that handlers read from.
    /// </summary>
    [Fact]
    public void HybridCache_Should_BeOneInstance_When_ResolvedDirectlyOrInjected()
    {
        HybridCache direct = Factory.Services.GetRequiredService<HybridCache>();
        using IServiceScope scope = Factory.Services.CreateScope();

        scope.ServiceProvider.GetServices<HybridCache>().ShouldHaveSingleItem().ShouldBeSameAs(direct);

        ProblemSeeder seeder = scope.ServiceProvider.GetRequiredService<ProblemSeeder>();
        object? injected = typeof(ProblemSeeder)
            .GetField("<cache>P", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(seeder);
        injected.ShouldBeSameAs(direct);
    }

    [Fact]
    public async Task HybridCache_Should_ReadThroughRedis_When_L1IsBypassed()
    {
        HybridCache cache = Factory.Services.GetRequiredService<HybridCache>();
        string key = $"caching-test:{Guid.NewGuid():N}";
        await cache.SetAsync(key, "from-l2");

        string? value = await cache.GetOrCreateAsync(
            key,
            _ => ValueTask.FromResult("factory-ran"),
            new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCache });

        value.ShouldBe("from-l2");
    }
}
