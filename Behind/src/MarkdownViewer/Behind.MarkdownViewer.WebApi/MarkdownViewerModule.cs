using Behind.Common.Modules;
using Behind.MarkdownViewer.Common;

namespace Behind.MarkdownViewer.WebApi;

public static class MarkdownViewerModule
{
    private const string Icon = "fa-book";
    private const int MenuPosition = 80;
    private const string ViewerController = "modules/markdownviewer/controllers/docs";
    private const string ManageController = "modules/markdownviewer/controllers/markdownmanager";
    private const string Style = "modules/markdownviewer/css/markdownviewer.css";

    public static ModuleDescriptor Descriptor { get; } = new(MarkdownViewerModuleNames.Key, "Markdown Viewer")
    {
        GrantAreas = [new ModuleGrantArea(MarkdownViewerModuleNames.Area, ModuleAudience.Administration)],
        MenuItems =
        [
            new ModuleMenuItem(
                MarkdownViewerModuleNames.MenuItemId,
                MarkdownViewerModuleNames.Name,
                MarkdownViewerModuleNames.PublicRoute,
                MenuPosition)
        ],
        AdministrationPages =
        [
            new ModuleAdministrationPage(
                MarkdownViewerModuleNames.AdministrationNameKey,
                Icon,
                MarkdownViewerModuleNames.ManageRoute,
                MarkdownViewerModuleNames.WriteGrant)
        ],
        Routes =
        [
            new ModuleRoute(MarkdownViewerModuleNames.PublicRoute, ViewerController),
            new ModuleRoute(MarkdownViewerModuleNames.PublicRoute + "/:path", ViewerController),
            new ModuleRoute(MarkdownViewerModuleNames.ManageRoute, ManageController)
        ],
        ConfigurationFiles = [MarkdownViewerModuleNames.ConfigurationFile],
        Frontend = new ModuleFrontend(typeof(MarkdownViewerModule).Assembly, [Style])
    };
}
