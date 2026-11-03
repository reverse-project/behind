namespace Behind.Tests.Helpers;

public static class BuiltInMenuTestExtensions
{
    extension(BuiltInMenu menu)
    {
        public bool HasItem(string title, string url, MenuItemKind kind) =>
            menu.Items.Any(item => item.Title == title && item.Url == url && item.Kind == kind);
    }
}
