using System.Diagnostics;
using Behind.Common.Logging;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Behind.Tests.Common;

public class CorrelationTelemetryTests
{
    private sealed class CapturingExporter : BaseExporter<LogRecord>
    {
        public List<IReadOnlyList<KeyValuePair<string, object?>>> Captured { get; } = [];

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var record in batch)
            {
                Captured.Add(record.Attributes?.ToList() ?? []);
            }

            return ExportResult.Success;
        }
    }

    [Fact]
    public void ActivityProcessor_TagsStartedActivityWithCurrentCorrelationId()
    {
        using var source = new ActivitySource("correlation-tests");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "correlation-tests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var scope = CorrelationContext.Begin("trace-1");
        using var activity = source.StartActivity("work");

        new CorrelationIdActivityProcessor().OnStart(activity!);

        Assert.Equal("trace-1", activity!.GetTagItem(CorrelationContext.TelemetryAttribute));
    }

    [Fact]
    public void Begin_TagsCurrentActivity()
    {
        using var source = new ActivitySource("correlation-begin-tests");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "correlation-begin-tests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("server");

        using var scope = CorrelationContext.Begin("server-1");

        Assert.Equal("server-1", activity!.GetTagItem(CorrelationContext.TelemetryAttribute));
    }

    [Fact]
    public void LogRecordProcessor_AddsCorrelationIdAttribute()
    {
        var exporter = new CapturingExporter();
        using var factory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
        {
            options.AddProcessor(new CorrelationIdLogRecordProcessor());
            options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
        }));

        using (CorrelationContext.Begin("log-1"))
        {
            factory.CreateLogger("test").LogInformation("hello {Name}", "world");
        }

        var attributes = Assert.Single(exporter.Captured);
        Assert.Contains(new KeyValuePair<string, object?>(CorrelationContext.TelemetryAttribute, "log-1"), attributes);
        Assert.Contains(attributes, a => a.Key == "Name" && Equals(a.Value, "world"));
    }
}
