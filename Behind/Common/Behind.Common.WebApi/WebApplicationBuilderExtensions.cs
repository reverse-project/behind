using Behind.Common.Configuration;
using Behind.Common.WebApi.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Instrumentation = Behind.Common.Configuration.OpenTelemetryConfiguration.Instrumentation;

namespace Behind.Common.WebApi;

public static class WebApplicationBuilderExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddOpenApi()
        {
            builder.Services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            });
            return builder;
        }
        
        public WebApplicationBuilder AddOpenTelemetry()
        {
            builder.Configuration.GetSection(OpenTelemetryConfiguration.SectionName)
                .Get<OpenTelemetryConfiguration>()
                .ApplyIfEnabled((configuration, openTelemetry) =>
                        configuration.ApplyTraces((exporter, instrumentation) =>
                                exporter.Apply(_ => openTelemetry.WithTracing(tracing =>
                                {
                                    Instrumentation.AspNetCoreInstrumentation.Apply(
                                        instrumentation,
                                        () => tracing.AddAspNetCoreInstrumentation()
                                    );
                                    Instrumentation.HttpClientInstrumentation.Apply(
                                        instrumentation,
                                        () => tracing.AddHttpClientInstrumentation()
                                    );
                                    Instrumentation.OtlpExporter.Apply(
                                        instrumentation,
                                        () => exporter.Endpoint.NotNull(e => tracing.AddOtlpExporter(options =>
                                            {
                                                options.Endpoint = e;
                                                exporter.Protocol.NotNull(p => options.Protocol = p);
                                            }),
                                            () => throw new InvalidOperationException(
                                                "OpenTelemetry Tracing OTLP exporter endpoint is required when OTLP exporter is enabled."
                                            ))
                                    );
                                })))
                            .ApplyMetrics((exporter, instrumentation) =>
                                exporter.Apply(_ =>
                                    openTelemetry.WithMetrics(metrics =>
                                        Instrumentation.AspNetCoreInstrumentation
                                            .ApplyChain(
                                                instrumentation,
                                                () => metrics.AddAspNetCoreInstrumentation(),
                                                Instrumentation.HttpClientInstrumentation
                                            )
                                            .ApplyChain(
                                                instrumentation,
                                                () => metrics.AddHttpClientInstrumentation(),
                                                Instrumentation.RuntimeInstrumentation
                                            )
                                            .ApplyChain(
                                                instrumentation,
                                                () => metrics.AddRuntimeInstrumentation(),
                                                Instrumentation.OtlpExporter
                                            ).Apply(
                                                instrumentation,
                                                () => exporter.Endpoint.NotNull(e => metrics.AddOtlpExporter(options =>
                                                    {
                                                        options.Endpoint = e;
                                                        exporter.Protocol.NotNull(p => options.Protocol = p);
                                                    }),
                                                    () => throw new InvalidOperationException(
                                                        "OpenTelemetry Metrics OTLP exporter endpoint is required when OTLP exporter is enabled."
                                                    ))))))
                            .ApplyLogging((loggingConfiguration, instrumentation) =>
                                loggingConfiguration.Apply(_ => openTelemetry.WithLogging(loggingBuilder =>
                                        Instrumentation.OtlpExporter.Apply(
                                            instrumentation,
                                            () => loggingConfiguration.Endpoint.NotNull(e =>
                                                    loggingBuilder.AddOtlpExporter(options =>
                                                    {
                                                        options.Endpoint = e;
                                                        loggingConfiguration.Protocol.NotNull(p =>
                                                            options.Protocol = p);
                                                    }),
                                                () => throw new InvalidOperationException(
                                                    "OpenTelemetry Logging OTLP exporter endpoint is required when OTLP exporter is enabled."
                                                ))),
                                    logging => loggingConfiguration
                                        .Apply(
                                            loggingConfiguration.IncludeFormattedMessage,
                                            v => logging.IncludeFormattedMessage = v.IsTrue()
                                        )
                                        .Apply(
                                            loggingConfiguration.IncludeScopes,
                                            v => logging.IncludeScopes = v.IsTrue()
                                        )
                                        .Apply(
                                            loggingConfiguration.ParseStateValues,
                                            v => logging.ParseStateValues = v.IsTrue()
                                        )))), builder.Services.AddOpenTelemetry()
                );

            return builder;
        }
    }
}