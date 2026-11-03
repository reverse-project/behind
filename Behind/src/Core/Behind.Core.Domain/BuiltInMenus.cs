namespace Behind.Core.Domain;

public sealed record BuiltInMenuItem(
    Guid Id,
    string Title,
    string Url,
    MenuItemKind Kind,
    int Position,
    IReadOnlyList<Guid> RoleIds);

public sealed record BuiltInMenu(Guid Id, string Name, string Title, IReadOnlyList<BuiltInMenuItem> Items);

public static class BuiltInMenus
{
    public const string MainName = "main";

    public static readonly BuiltInMenu Main = new(
        Guid.Parse("80000000-0000-0000-0000-000000000001"),
        MainName,
        "Main menu",
        [
            Link(1, "Index", "#/", 10),
            Link(2, "News", "#/news", 20),
            Link(3, "Articles", "#/articles", 30),
            Link(9, "Galleries", "#/galleries", 35),
            Link(10, "Downloads", "#/downloads", 37),
            Link(4, "Wall of shame", "#/wall-of-shame", 40),
            User(5, "Profile", "#/profile", 10),
            User(6, "Session", "#/session", 20),
            User(7, "Sessions", "#/sessions", 30),
            User(
                8,
                "Administration",
                "#/administration",
                40,
                BuiltInAccess.Administrators.Id,
                BuiltInAccess.Developers.Id)
        ]);

    public static readonly IReadOnlyList<BuiltInMenu> All = [Main];

    public static bool IsBuiltIn(Guid menuId) => All.Any(menu => menu.Id == menuId);

    public static bool IsMain(Guid menuId) => menuId == Main.Id;

    private static BuiltInMenuItem Link(int number, string title, string url, int position) =>
        Item(number, title, url, MenuItemKind.Link, position, []);

    private static BuiltInMenuItem User(int number, string title, string url, int position, params Guid[] roleIds) =>
        Item(number, title, url, MenuItemKind.User, position, roleIds);

    private static BuiltInMenuItem Item(
        int number,
        string title,
        string url,
        MenuItemKind kind,
        int position,
        IReadOnlyList<Guid> roleIds) =>
        new(Guid.Parse($"81000000-0000-0000-0000-{number:D12}"), title, url, kind, position, roleIds);
}
