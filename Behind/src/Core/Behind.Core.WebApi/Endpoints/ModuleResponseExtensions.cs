using Behind.Common.Modules;

namespace Behind.Core.WebApi.Endpoints;

public static class ModuleResponseExtensions
{
    extension(ModuleDescriptor module)
    {
        public ModuleResponse ToResponse() =>
            new(
                module.Key,
                module.Name,
                module.FrontendPath,
                [..module.Routes.Select(route => new ModuleRouteResponse(route.Path, route.Controller, route.Open))],
                [
                    ..module.AdministrationPages.Select(page =>
                        new ModuleAdministrationPageResponse(page.Name, page.Icon, page.Href, page.Grant))
                ],
                [..module.Frontend.AsEnumerable().SelectMany(frontend => frontend.Styles)],
                [..module.Widgets.Select(widget => new ModuleWidgetResponse(widget.Key, widget.Name, widget.Model))]);
    }

    extension(ModuleCatalog catalog)
    {
        public ModulesResponse ToResponse() => new([..catalog.Modules.Select(module => module.ToResponse())]);
    }
}
