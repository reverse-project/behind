using System.Net;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Middleware;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Behind.Tests.CoreWebApi.Middleware;

public sealed class DynamicForwardedHeadersMiddlewareTests
{
    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("::ffff:0.0.0.0/96")]
    [InlineData("172.18.0.0/16")]
    [InlineData("2001:db8::/48")]
    [InlineData("172.18.0.10/24")]
    [InlineData("127.1")]
    [InlineData("172.18.0.10/33")]
    [InlineData("garbage")]
    public void InvalidEntryRejectsWholePolicy(string entry)
    {
        Assert.False(ForwardedHeadersPolicy.TryCreate(new ForwardedHeadersConfiguration
        {
            Enabled = true,
            KnownForwarders = ["172.18.0.10", entry]
        }, out _));
    }

    [Theory]
    [InlineData("::ffff:172.18.0.10", "::ffff:172.18.0.0/120", 2, "203.0.113.9", "172.18.0.11", "203.0.113.9")]
    [InlineData("172.18.0.10", "172.18.0.10", 2, "203.0.113.9", "198.51.100.7", "198.51.100.7")]
    [InlineData("2001:db8::1", "2001:db8::/64", 1, "2001:db8:2::2", "2001:db8::3", "2001:db8::3")]
    [InlineData("172.18.0.9", "172.18.0.10", 2, "203.0.113.9", "172.18.0.10", "172.18.0.9")]
    [InlineData("172.18.0.10", "172.18.0.10", 2, "203.0.113.9", "unknown", "172.18.0.10")]
    public async Task ResolvesOnlyThroughTrustedHops(string peer, string known, int limit,
        string left, string right, string expected)
    {
        var configuration = Configuration(known, limit);
        using var services = Services(configuration);
        Assert.Equal(IPAddress.Parse(expected), await Request(services, peer, new StringValues([left, right])));
        Assert.Equal(IPAddress.Parse(expected), await Request(services, peer, $"{left}, {right}"));
    }

    [Fact]
    public async Task ChangesTrustAfterReloadAndFailsClosedForUnbindableLimit()
    {
        var configuration = Configuration("172.18.0.10", 1);
        using var services = Services(configuration);
        const string peer = "172.18.0.10";
        const string client = "203.0.113.9";
        Assert.Equal(IPAddress.Parse(client), await Request(services, peer, client));
        configuration[$"{ForwardedHeadersConfiguration.SectionName}:Enabled"] = "false";
        configuration.Reload();
        Assert.Equal(IPAddress.Parse(peer), await Request(services, peer, client));
        configuration[$"{ForwardedHeadersConfiguration.SectionName}:Enabled"] = "true";
        configuration[$"{ForwardedHeadersConfiguration.SectionName}:ForwardLimit"] = "wrong";
        configuration.Reload();
        Assert.Equal(IPAddress.Parse(peer), await Request(services, peer, client));
    }

    private static IConfigurationRoot Configuration(string known, int limit)
    {
        var section = ForwardedHeadersConfiguration.SectionName;
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{section}:Enabled"] = "true",
            [$"{section}:KnownForwarders:0"] = known,
            [$"{section}:ForwardLimit"] = limit.ToString()
        }).Build();
    }

    private static ServiceProvider Services(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddReloadableOptions(configuration, ForwardedHeadersPolicy.Read);
        return services.BuildServiceProvider();
    }

    private static async Task<IPAddress?> Request(ServiceProvider services, string peer, StringValues chain)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Headers["X-Forwarded-For"] = chain;
        IPAddress? observed = null;
        var middleware = new DynamicForwardedHeadersMiddleware(current =>
        {
            observed = current.Connection.RemoteIpAddress;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context, services.GetRequiredService<IOptionsMonitor<ForwardedHeadersPolicy>>());
        return observed;
    }
}
