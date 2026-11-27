using Behind.Common.Modules;
using Behind.Core.Domain;

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
        public ModulesResponse ToResponse(AccessCatalog access, ModuleAvailability availability)
        {
            var active = availability.Active(catalog);
            var activeKeys = active.Modules.Select(module => module.Key).ToHashSet(StringComparer.Ordinal);
            var disabledAreas = catalog.Modules
                .Where(module => !activeKeys.Contains(module.Key))
                .SelectMany(module => module.GrantAreas)
                .Select(area => area.Area)
                .ToHashSet(StringComparer.Ordinal);

            return new(
                [..active.Modules.Select(module => module.ToResponse())],
                [..access.Grants.Where(grant => !disabledAreas.Contains(grant.Area)).Select(grant => grant.Grant.Name)]);
        }
    }
}
