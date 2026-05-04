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
                        c.ApplyTraces((oec, i) =>
                                oec.Apply(_ => b.WithTracing(t =>
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
                                        () => oec.Endpoint.NotNull(e =>
                                        {
                                            t.AddOtlpExporter(options =>
                                            {
                                                options.Endpoint = e;
                                                oec.Protocol.NotNull(p => options.Protocol = p);
                                            });
                                        })
                                    );
                                })))
                            .ApplyMetrics((oec, i) =>
                                oec.Apply(_ =>
                                    b.WithMetrics(m =>
                                        OpenTelemetryConfiguration.Instrumentation.AspNetCoreInstrumentation
                                            .ApplyChain(
                                                i,
                                                () => m.AddAspNetCoreInstrumentation(),
                                                OpenTelemetryConfiguration.Instrumentation.HttpClientInstrumentation
                                            )
                                            .ApplyChain(
                                                i,
                                                () => m.AddHttpClientInstrumentation(),
                                                OpenTelemetryConfiguration.Instrumentation.RuntimeInstrumentation
                                            )
                                            .ApplyChain(
                                                i,
                                                () => m.AddRuntimeInstrumentation(),
                                                OpenTelemetryConfiguration.Instrumentation.OtlpExporter
                                            ).Apply(
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
                                            ))))
                            .ApplyLogging((lc, i) =>
                                lc.Apply(_ => b.WithLogging(l =>
                                        OpenTelemetryConfiguration.Instrumentation.OtlpExporter.Apply(
                                            i,
                                            () => lc.Endpoint.NotNull(e =>
                                            {
                                                l.AddOtlpExporter(options =>
                                                {
                                                    options.Endpoint = e;
                                                    lc.Protocol.NotNull(p => options.Protocol = p);
                                                });
                                            })),
                                    logging =>
                                    {
                                        lc
                                            .Apply(
                                                lc.IncludeFormattedMessage,
                                                v => logging.IncludeFormattedMessage = v.IsTrue()
                                            )
                                            .Apply(
                                                lc.IncludeScopes,
                                                v => logging.IncludeScopes = v.IsTrue()
                                            )
                                            .Apply(
                                                lc.ParseStateValues,
                                                v => logging.ParseStateValues = v.IsTrue()
                                            );
                                    }))), builder.Services.AddOpenTelemetry()
                );

            return builder;
        }
    }
}