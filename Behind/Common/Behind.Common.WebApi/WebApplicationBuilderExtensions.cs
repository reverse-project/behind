using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Behind.Common.WebApi;

public static class WebApplicationBuilderExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddOpenTelemetry()
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(t => t.AddAspNetCoreInstrumentation().AddOtlpExporter())
                .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddOtlpExporter())
                .WithLogging(l => l.AddOtlpExporter());

            return builder;
        }
    }
}