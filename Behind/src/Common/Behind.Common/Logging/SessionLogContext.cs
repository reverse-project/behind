namespace Behind.Common.Logging;

public sealed record SessionLogInformation(string Locale, Guid? SessionId, Guid? UserId, Guid? ImpersonatorId);

public static class SessionLogContext
{
    private static readonly AsyncLocal<SessionLogInformation?> Ambient = new();

    public static SessionLogInformation? Current => Ambient.Value;

    public static IDisposable Begin(SessionLogInformation information)
    {
        var scope = new Scope(Ambient.Value);
        Ambient.Value = information;
        return scope;
    }

    private sealed record Scope(SessionLogInformation? Previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = Previous;
    }
}
