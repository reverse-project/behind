using Behind.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
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
            builder.Configuration.GetSection(OpenTelemetryConfiguration.SectionName)
                .Get<OpenTelemetryConfiguration>()
                .Apply((c, b) =>
                    {
                        c.ApplyTraces((oec, i) =>
                                b.WithTracing(t =>
                                {
                                    OpenTelemetryConfiguration.Instrumentation.AspNetCoreInstrumentation.Apply(
                                        i,
                                        () => t.AddAspNetCoreInstrumentation()
                                    );
                                    OpenTelemetryConfiguration.Instrumentation.HttpClientInstrumentation.Apply(
                                        i,
                                        () => t.AddHttpClientInstrumentation()
                                    );
                                    OpenTelemetryConfiguration.Instrumentation.OtlpExporter.Apply(
                                        i,
                                        () => c.Traces.Endpoint.NotNull(e =>
                                        {
                                            t.AddOtlpExporter(options =>
                                            {
                                                options.Endpoint = e;
                                                oec.Protocol.NotNull(p => options.Protocol = p);
                                            });
                                        })
                                    );
                                })
                            )
                            .ApplyMetrics((oec, i) =>
                                b.WithMetrics(m =>
                                {
                                    OpenTelemetryConfiguration.Instrumentation.AspNetCoreInstrumentation.Apply(
                                        i,
                                        () => m.AddAspNetCoreInstrumentation()
                                    );
                                    OpenTelemetryConfiguration.Instrumentation.HttpClientInstrumentation.Apply(
                                        i,
                                        () => m.AddHttpClientInstrumentation()
                                    );
                                    OpenTelemetryConfiguration.Instrumentation.RuntimeInstrumentation.Apply(
                                        i,
                                        () => m.AddRuntimeInstrumentation()
                                    );
                                    OpenTelemetryConfiguration.Instrumentation.OtlpExporter.Apply(
                                        i,
                                        () =>
                                        {
                                            oec.Endpoint.NotNull(e =>
                                            {
                                                m.AddOtlpExporter(options =>
                                                {
                                                    options.Endpoint = e;
                                                    oec.Protocol.NotNull(p => options.Protocol = p);
                                                });
                                            });
                                        }
                                    );
                                }))
                            .ApplyLogging((lc, i) =>
                                b.WithLogging(l =>
                                {
                                    OpenTelemetryConfiguration.Instrumentation.OtlpExporter.Apply(
                                        i,
                                        () => lc.Endpoint.NotNull(e =>
                                        {
                                            l.AddOtlpExporter(options =>
                                            {
                                                options.Endpoint = e;
                                                lc.Protocol.NotNull(p => options.Protocol = p);
                                            });
                                        })
                                    );
                                }));
                    }, builder.Services.AddOpenTelemetry()
                );

            return builder;
        }
    }
}