using Behind.Common.Modules;

namespace Behind.Quotes.Common;

public static class QuotesModuleNames
{
    public const string Key = "quotes";
    public const string Name = "Quotes";
    public const string Area = "Quotes";
    public const string PublicRoute = "#/quotes";
    public const string ManageRoute = "#/manage/quotes";
    public const string AdministrationNameKey = "quotes.administration";
    public const string ReadGrant = Area + "." + GrantActions.Read;
    public const string WriteGrant = Area + "." + GrantActions.Write;
    public const string DeleteGrant = Area + "." + GrantActions.Delete;

    public static readonly Guid MenuItemId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
}
