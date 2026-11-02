namespace Behind.Core.Domain;

public static class GrantArea
{
    public const string Session = nameof(Session);
    public const string AuditLog = nameof(AuditLog);
    public const string Role = nameof(Role);
    public const string User = nameof(User);
    public const string Configuration = nameof(Configuration);
    public const string Impersonation = nameof(Impersonation);
    public const string Site = nameof(Site);
    public const string News = nameof(News);
    public const string Article = nameof(Article);

    public const string RawDatabase = nameof(RawDatabase);

    public static readonly IReadOnlyList<string> All = [Session, AuditLog, Role, User, Configuration, Impersonation, Site, News, Article];
}

public static class GrantAction
{
    public const string Read = nameof(Read);
    public const string Write = nameof(Write);
    public const string Delete = nameof(Delete);
    public const string Manage = nameof(Manage);

    public static readonly IReadOnlyList<string> All = [Read, Write, Delete, Manage];
}

public static class GrantName
{
    public static string Of(string area, string action) => $"{area}.{action}";
}
