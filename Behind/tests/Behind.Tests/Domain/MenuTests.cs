namespace Behind.Tests.Domain;

public class MenuTests
{
    [Fact]
    public void Menu_NewMenu_HasAnEmptyIdAndTheGivenNameAndTitle()
    {
        var menu = new Menu("footer", "Footer");

        Assert.Equal(new VersionedKey<Guid>(Guid.Empty), menu.Id);
        Assert.Equal("footer", menu.Name);
        Assert.Equal("Footer", menu.Title);
        Assert.Equal("Menu", Menu.EntityType);
    }

    [Fact]
    public void MenuItem_NewItem_HasAnEmptyIdAndTheGivenFields()
    {
        var item = new MenuItem("Docs", "#/docs", MenuTarget.NewTab, MenuItemKind.Link, 10);

        Assert.Equal(new VersionedKey<Guid>(Guid.Empty), item.Id);
        Assert.Equal(("Docs", "#/docs", MenuTarget.NewTab, MenuItemKind.Link, 10), (item.Title, item.Url, item.Target, item.Kind, item.Position));
        Assert.Equal("MenuItem", MenuItem.EntityType);
    }

    [Theory]
    [InlineData(MenuTarget.CurrentPage, "\"CurrentPage\"")]
    [InlineData(MenuTarget.NewTab, "\"NewTab\"")]
    [InlineData(MenuTarget.NewWindow, "\"NewWindow\"")]
    public void MenuTarget_IsSerializedByName(MenuTarget target, string json)
    {
        Assert.Equal(json, System.Text.Json.JsonSerializer.Serialize(target));
        Assert.Equal(target, System.Text.Json.JsonSerializer.Deserialize<MenuTarget>(json));
    }

    [Theory]
    [InlineData(MenuItemKind.Link, "\"Link\"")]
    [InlineData(MenuItemKind.User, "\"User\"")]
    public void MenuItemKind_IsSerializedByName(MenuItemKind kind, string json)
    {
        Assert.Equal(json, System.Text.Json.JsonSerializer.Serialize(kind));
        Assert.Equal(kind, System.Text.Json.JsonSerializer.Deserialize<MenuItemKind>(json));
    }

    [Fact]
    public void MenuTarget_UnknownName_IsNotAccepted()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => System.Text.Json.JsonSerializer.Deserialize<MenuTarget>("\"Popup\""));
    }
}
