using Serilog.Core;
using Serilog.Events;

namespace Behind.Common.Logging;

public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) =>
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty(SerilogExtensions.CorrelationIdProperty, CorrelationContext.Current));
}
