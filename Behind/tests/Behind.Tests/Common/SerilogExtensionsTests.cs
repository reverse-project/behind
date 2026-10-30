using System.Text.RegularExpressions;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Tests.Common;

public class SerilogExtensionsTests
{
    private static async Task<string> CaptureAsync(Func<IHost, Task> act, IDictionary<string, string?>? overrides = null)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Configuration.AddInMemoryCollection(overrides ?? new Dictionary<string, string?>());
            builder.AddSerilog();

            using var host = builder.Build();
            await act(host);
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }

    [Fact]
    public async Task AddSerilog_UsesIsoTimestampAndLevel()
    {
        var output = await CaptureAsync(host =>
        {
            host.Services.GetRequiredService<ILogger<SerilogExtensionsTests>>().LogInformation("hello");
            return Task.CompletedTask;
        });

        Assert.Matches(
            new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}[+-]\d{2}:\d{2} \[INF\] \[" + Regex.Escape(CorrelationContext.ProcessId) + @"\] .+: hello"),
            output);
    }

    [Fact]
    public async Task AddSerilog_IncludesCorrelationIdFromMiddleware()
    {
        var output = await CaptureAsync(async host =>
        {
            var logger = host.Services.GetRequiredService<ILogger<SerilogExtensionsTests>>();
            var middleware = new CorrelationIdMiddleware(_ =>
            {
                logger.LogInformation("inside");
                return Task.CompletedTask;
            }, CorrelationIdOptions.Default);

            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationIdOptions.DefaultHeaderName] = "abc-123";
            await middleware.InvokeAsync(context);
            logger.LogInformation("outside");
        });

        Assert.Contains("[INF] [abc-123]", output);
        Assert.Contains($"[INF] [{CorrelationContext.ProcessId}] ", output);
    }

    [Fact]
    public async Task AddSerilog_JsonConfigurationOverridesDefaults()
    {
        var output = await CaptureAsync(host =>
        {
            var logger = host.Services.GetRequiredService<ILogger<SerilogExtensionsTests>>();
            logger.LogInformation("hidden");
            logger.LogWarning("shown");
            return Task.CompletedTask;
        }, new Dictionary<string, string?> { ["Serilog:MinimumLevel:Default"] = "Warning" });

        Assert.DoesNotContain("hidden", output);
        Assert.Contains("shown", output);
    }
}
