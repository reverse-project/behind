using Behind.Common.Modules;

namespace Behind.MarkdownViewer.Common;

public static class MarkdownViewerModuleNames
{
    public const string Key = "markdownviewer";
    public const string Name = "Documentation";
    public const string Area = "MarkdownViewer";
    public const string PublicRoute = "#/docs";
    public const string ManageRoute = "#/manage/markdown-viewer";
    public const string ConfigurationFile = "markdownviewer.json";
    public const string AdministrationNameKey = "markdownviewer.administration";
    public const string ReadGrant = Area + "." + GrantActions.Read;
    public const string WriteGrant = Area + "." + GrantActions.Write;

    public static readonly Guid MenuItemId = Guid.Parse("a3000000-0000-0000-0000-000000000001");
}
