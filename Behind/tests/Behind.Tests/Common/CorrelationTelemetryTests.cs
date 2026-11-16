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
    public void SessionLogProcessor_ExportsValidatedSessionAttributesWithoutLoggerScopes()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var impersonatorId = Guid.NewGuid();
        var exporter = new CapturingExporter();
        using var factory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = false;
            options.AddProcessor(new SessionLogRecordProcessor());
            options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
        }));
        using (SessionLogContext.Begin(new SessionLogInformation("hu-HU", sessionId, userId, impersonatorId)))
        {
            factory.CreateLogger("test").LogInformation("hello {Name}", "world");
        }

        var attributes = Assert.Single(exporter.Captured);
        Assert.Contains(new KeyValuePair<string, object?>("session.locale", "hu-HU"), attributes);
        Assert.Contains(new KeyValuePair<string, object?>("session.id", sessionId.ToString()), attributes);
        Assert.Contains(new KeyValuePair<string, object?>("user.id", userId.ToString()), attributes);
        Assert.Contains(new KeyValuePair<string, object?>("session.impersonator.id", impersonatorId.ToString()), attributes);
        Assert.Contains(new KeyValuePair<string, object?>("session.impersonated", true), attributes);
        Assert.Contains(attributes, a => a.Key == "Name" && Equals(a.Value, "world"));
    }

    [Fact]
    public void SessionLogProcessor_UsesAuthoritativeAttributesAndDoesNotLeakContext()
    {
        var exporter = new CapturingExporter();
        using var factory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
        {
            options.AddProcessor(new SessionLogRecordProcessor());
            options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
        }));
        var logger = factory.CreateLogger("test");
        using (SessionLogContext.Begin(new SessionLogInformation("en-US", null, null, null)))
        {
            logger.LogInformation("Locale {session.locale} and user {user.id}", "forged", "forged");
        }
        logger.LogInformation("outside");

        Assert.Equal(2, exporter.Captured.Count);
        Assert.Contains(new KeyValuePair<string, object?>("session.locale", "en-US"), exporter.Captured[0]);
        Assert.DoesNotContain(exporter.Captured[0], a => a.Key == "user.id");
        Assert.DoesNotContain(exporter.Captured[1], a => a.Key.StartsWith("session.", StringComparison.Ordinal));
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
