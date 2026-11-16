using System.Text.Json.Serialization;
using Behind.Common;

namespace Behind.Core.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<MenuTarget>))]
public enum MenuTarget
{
    CurrentPage,
    NewTab,
    NewWindow
}

[JsonConverter(typeof(JsonStringEnumConverter<MenuItemKind>))]
public enum MenuItemKind
{
    Link,
    User
}

public sealed record Menu(VersionedKey<Guid> Id, string Name, string Title)
{
    public const string EntityType = nameof(Menu);
    public const int MaxNameLength = 100;
    public const int MaxTitleLength = 200;

    public Menu(string name, string title) : this(new VersionedKey<Guid>(Guid.Empty), name, title)
    {
    }
}

public sealed record MenuItem(
    VersionedKey<Guid> Id,
    string Title,
    string Url,
    MenuTarget Target,
    MenuItemKind Kind,
    int Position)
{
    public const string EntityType = nameof(MenuItem);
    public const int MaxTitleLength = 200;
    public const int MaxUrlLength = 2_000;

    public MenuItem(string title, string url, MenuTarget target, MenuItemKind kind, int position)
        : this(new VersionedKey<Guid>(Guid.Empty), title, url, target, kind, position)
    {
    }
}
