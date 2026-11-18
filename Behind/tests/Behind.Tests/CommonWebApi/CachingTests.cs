using Behind.Common.Configuration;
using Behind.Common.WebApi;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CommonWebApi;

public sealed class CachingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Memory")]
    [InlineData("RedisCompatible")]
    public async Task UnconfiguredRedisUsesMemoryAndExecutesFactoryOnce(string? provider)
    {
        var builder = Host.CreateApplicationBuilder();
        if (provider is not null)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{CachingConfiguration.SectionName}:Provider"] = provider
            });
        }
        builder.AddCaching();
        using var host = builder.Build();
        Assert.Null(host.Services.GetService<IDistributedCache>());
        var cache = host.Services.GetRequiredService<HybridCache>();
        var calls = 0;
        ValueTask<string> Factory(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult("cached");
        }

        Assert.Equal("cached", await cache.GetOrCreateAsync("memory", Factory));
        Assert.Equal("cached", await cache.GetOrCreateAsync("memory", Factory));
        Assert.Equal(1, calls);
        await cache.RemoveAsync("memory");
        Assert.Equal("cached", await cache.GetOrCreateAsync("memory", Factory));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void KeyResolvesConnectionStringsAndIgnoresLegacyInlineConnection()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Caching:Provider"] = "RedisCompatible",
            ["Caching:Key"] = "Valkey",
            ["Caching:ConnectionString"] = "must-not-be-used:6379",
            ["ConnectionStrings:Valkey"] = "localhost:6379"
        });
        builder.AddCaching();
        using var host = builder.Build();
        Assert.NotNull(host.Services.GetService<IDistributedCache>());
        var redis = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<
            Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>>();
        Assert.Equal("localhost:6379", redis.Value.Configuration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Missing")]
    public void MissingNamedConnectionFallsBackToMemory(string? key)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Caching:Provider"] = "RedisCompatible",
            ["Caching:Key"] = key
        });
        builder.AddCaching();
        using var host = builder.Build();
        Assert.Null(host.Services.GetService<IDistributedCache>());
    }

    [Fact]
    public void InvalidProviderFailsExplicitly()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{CachingConfiguration.SectionName}:Provider"] = "999"
        });

        Assert.Throws<FormatException>(() => builder.AddCaching());
    }
}
