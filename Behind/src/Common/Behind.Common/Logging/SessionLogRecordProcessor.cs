using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Behind.Common.Logging;

public sealed class SessionLogRecordProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        if (SessionLogContext.Current is not { } session)
        {
            return;
        }

        var attributes = (data.Attributes ?? []).Where(attribute => attribute.Key is not
            ("session.locale" or "session.id" or "user.id" or "session.impersonated" or "session.impersonator.id"))
            .ToList();
        attributes.Add(new("session.locale", session.Locale));
        if (session.SessionId is { } sessionId)
        {
            attributes.Add(new("session.id", sessionId.ToString()));
            attributes.Add(new("session.impersonated", session.ImpersonatorId.HasValue));
        }
        if (session.UserId is { } userId)
        {
            attributes.Add(new("user.id", userId.ToString()));
        }
        if (session.ImpersonatorId is { } impersonatorId)
        {
            attributes.Add(new("session.impersonator.id", impersonatorId.ToString()));
        }
        data.Attributes = attributes;
    }
}
