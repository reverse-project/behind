using Behind.Common.Modules;

namespace Behind.Sport.Common;

public static class SportModuleNames
{
    public const string Key = "sport";
    public const string Name = "Sport";
    public const string Area = "Sport";
    public const string PublicRoute = "#/sport";
    public const string ManageRoute = "#/manage/sport";
    public const string AdministrationNameKey = "sport.administration";
    public const string ReadGrant = Area + "." + GrantActions.Read;
    public const string WriteGrant = Area + "." + GrantActions.Write;
    public const string DeleteGrant = Area + "." + GrantActions.Delete;
    public const long ImageLimit = 5 * 1024 * 1024;

    public static readonly Guid MenuItemId = Guid.Parse("a4000000-0000-0000-0000-000000000001");
}
