using System.Diagnostics;
using OpenTelemetry;

namespace Behind.Common.Logging;

public sealed class CorrelationIdActivityProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data) =>
        data.SetTag(CorrelationContext.TelemetryAttribute, CorrelationContext.Current);
}
