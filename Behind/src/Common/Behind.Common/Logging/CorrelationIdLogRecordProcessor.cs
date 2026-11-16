using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Behind.Common.Logging;

public sealed class CorrelationIdLogRecordProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data) =>
        data.Attributes =
        [
            ..data.Attributes ?? [],
            new KeyValuePair<string, object?>(CorrelationContext.TelemetryAttribute, CorrelationContext.Current)
        ];
}
