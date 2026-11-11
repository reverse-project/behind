using Behind.Common.Modules;

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
    public const string Menu = nameof(Menu);
    public const string Gallery = nameof(Gallery);
    public const string Download = nameof(Download);
    public const string RouteAlias = nameof(RouteAlias);
    public const string Access = nameof(Access);
    public const string Chat = nameof(Chat);

    public const string RawDatabase = nameof(RawDatabase);

    public static readonly IReadOnlyList<string> All = [Session, AuditLog, Role, User, Configuration, Impersonation, Site, News, Article, Menu, Gallery, Download, RouteAlias, Access, Chat];

    public static IReadOnlyList<string> ActionsOf(string area) =>
        area == Impersonation ? [GrantAction.Manage] : GrantAction.All;
}

public static class GrantAction
{
    public const string Read = GrantActions.Read;
    public const string Write = GrantActions.Write;
    public const string Delete = GrantActions.Delete;
    public const string Manage = GrantActions.Manage;

    public static readonly IReadOnlyList<string> All = GrantActions.All;
}

public static class GrantName
{
    public static string Of(string area, string action) => $"{area}.{action}";
}
