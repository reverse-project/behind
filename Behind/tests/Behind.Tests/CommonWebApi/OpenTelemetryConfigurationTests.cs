using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Behind.Common.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Instrumentation = Behind.Common.Configuration.OpenTelemetryConfiguration.Instrumentation;

namespace Behind.Tests.CommonWebApi;

public class OpenTelemetryConfigurationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("UnknownInstrumentation")]
    [InlineData("otlpexporter")]
    public void AddOpenTelemetry_RejectsUnknownInstrumentation(string name)
    {
        var builder = CreateBuilder();
        builder.Configuration["OpenTelemetry:Traces:Instruments:0"] = name;

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddOpenTelemetry());

        Assert.Contains("Unknown OpenTelemetry instrumentation", exception.ToString());
    }

    [Fact]
    public void InstrumentationConverter_RejectsUnsupportedTypes()
    {
        var converter = TypeDescriptor.GetConverter(typeof(Instrumentation));

        Assert.True(converter.CanConvertFrom(typeof(string)));
        Assert.False(converter.CanConvertFrom(typeof(int)));
        Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(42));
    }

    [Fact]
    public async Task AddOpenTelemetry_ExportsAllSignalsToConfiguredEndpoints()
    {
        const string sourceName = "Behind.Tests.OpenTelemetryConfiguration";
        var requests = new ConcurrentQueue<Uri>();
        var builder = CreateBuilder();
        builder.AddOpenTelemetry();
        builder.Services.Configure<OtlpExporterOptions>(options =>
            options.HttpClientFactory = () => new HttpClient(new CapturingHandler(requests)));
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(sourceName));
        builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(sourceName));
        await using var application = builder.Build();
        var traces = application.Services.GetRequiredService<TracerProvider>();
        var metrics = application.Services.GetRequiredService<MeterProvider>();
        var loggerProvider = application.Services.GetRequiredService<LoggerProvider>();

        foreach (var resource in new[] { traces.GetResource(), metrics.GetResource(), loggerProvider.GetResource() })
        {
            Assert.Contains(resource.Attributes, attribute => attribute.Key == "service.name" && Equals(attribute.Value, "telemetry-test"));
        }

        using var source = new ActivitySource(sourceName);
        using (var activity = source.StartActivity("test-span"))
        {
            Assert.NotNull(activity);
        }

        using var meter = new Meter(sourceName);
        meter.CreateCounter<long>("test.requests").Add(1);
        application.Services.GetRequiredService<ILoggerFactory>().CreateLogger(sourceName)
            .LogWarning("Telemetry export test");

        Assert.True(traces.ForceFlush(5000));
        Assert.True(metrics.ForceFlush(5000));
        Assert.True(loggerProvider.ForceFlush(5000));
        Assert.Contains(new Uri("http://otel.test/v1/traces"), requests);
        Assert.Contains(new Uri("http://otel.test/v1/metrics"), requests);
        Assert.Contains(new Uri("http://otel.test/v1/logs"), requests);
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenTelemetry:ServiceName"] = "telemetry-test",
            ["OpenTelemetry:IsEnabled"] = "true",
            ["OpenTelemetry:Traces:IsEnabled"] = "true",
            ["OpenTelemetry:Traces:Endpoint"] = "http://otel.test/v1/traces",
            ["OpenTelemetry:Traces:Protocol"] = "HttpProtobuf",
            ["OpenTelemetry:Traces:Instruments:0"] = "OtlpExporter",
            ["OpenTelemetry:Metrics:IsEnabled"] = "true",
            ["OpenTelemetry:Metrics:Endpoint"] = "http://otel.test/v1/metrics",
            ["OpenTelemetry:Metrics:Protocol"] = "HttpProtobuf",
            ["OpenTelemetry:Metrics:Instruments:0"] = "OtlpExporter",
            ["OpenTelemetry:Logging:IsEnabled"] = "true",
            ["OpenTelemetry:Logging:Endpoint"] = "http://otel.test/v1/logs",
            ["OpenTelemetry:Logging:Protocol"] = "HttpProtobuf",
            ["OpenTelemetry:Logging:Instruments:0"] = "OtlpExporter"
        });

        return builder;
    }

    private sealed class CapturingHandler(ConcurrentQueue<Uri> requests) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("application/x-protobuf", request.Content?.Headers.ContentType?.MediaType);
            requests.Enqueue(request.RequestUri!);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }
}
