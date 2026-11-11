using System.Reflection;
using System.Text.RegularExpressions;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Modules;

public static class ModuleCatalogExtensions
{
    private const string FrontendNamespace = "modules";
    private const string RoutePrefix = "#/";

    private static readonly Regex KeyPattern = new(
        "^[a-z][a-z0-9-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex AreaPattern = new(
        "^[A-Z][A-Za-z0-9]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ConfigurationFilePattern = new(
        "^[a-z][a-z0-9]*\\.json$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex WidgetModelPattern = new(
        "^[A-Za-z0-9/_-]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    extension(ModuleCatalog)
    {
        public static Validation<ModuleCatalog> Create(IEnumerable<ModuleDescriptor> modules)
        {
            var listed = modules.ToList();
            var problems = listed.SelectMany(module => module.Problems).Concat(listed.CrossProblems).ToList();

            return problems.Count == 0
                ? Valid(new ModuleCatalog(listed))
                : Invalid<ModuleCatalog>(problems.Select(problem => Error(problem)));
        }
    }

    extension(ModuleCatalog catalog)
    {
        public IReadOnlyList<ModuleGrantArea> GrantAreas =>
            [..catalog.Modules.SelectMany(module => module.GrantAreas)];

        public IReadOnlyList<ModuleMenuItem> MenuItems =>
            [..catalog.Modules.SelectMany(module => module.MenuItems)];

        public IReadOnlyList<ModuleAdministrationPage> AdministrationPages =>
            [..catalog.Modules.SelectMany(module => module.AdministrationPages)];

        public IReadOnlyList<ModuleWidget> Widgets =>
            [..catalog.Modules.SelectMany(module => module.Widgets)];

        public IReadOnlyList<string> ConfigurationFiles =>
            [..catalog.Modules.SelectMany(module => module.ConfigurationFiles)];

        public IReadOnlyList<ModuleFrontend> Frontends =>
            [..catalog.Modules.SelectMany(module => module.Frontend.AsEnumerable())];

        public IReadOnlyList<ModuleDatabase> Databases =>
            [..catalog.Modules.SelectMany(module => module.Database.AsEnumerable())];

        public IReadOnlyList<Assembly> ConfigurationAssemblies =>
            [..catalog.Databases.Select(database => database.ConfigurationAssembly).Distinct()];

        public IReadOnlyList<Type> SoftDeletedEntities =>
            [..catalog.Databases.SelectMany(database => database.SoftDeletedEntities).Distinct()];

        public IReadOnlySet<string> ReadOnlyEntities =>
            catalog.Databases.SelectMany(database => database.ReadOnlyEntities).ToHashSet(StringComparer.Ordinal);
    }

    extension(ModuleDescriptor module)
    {
        public string FrontendPath => $"{FrontendNamespace}/{module.Key}/";

        private IEnumerable<string> Problems =>
        [
            ..Require(KeyPattern.IsMatch(module.Key), $"Module key '{module.Key}' must be lowercase letters, digits and dashes"),
            ..Require(!string.IsNullOrWhiteSpace(module.Name), $"Module '{module.Key}' needs a name"),
            ..module.GrantAreas.SelectMany(area => area.ProblemsIn(module)),
            ..module.MenuItems.SelectMany(item => item.ProblemsIn(module)),
            ..module.AdministrationPages.SelectMany(page => page.ProblemsIn(module)),
            ..module.Routes.SelectMany(route => route.ProblemsIn(module)),
            ..module.Widgets.SelectMany(widget => widget.ProblemsIn(module)),
            ..module.ConfigurationFiles.SelectMany(file => Require(
                ConfigurationFilePattern.IsMatch(file),
                $"Configuration file '{file}' of module '{module.Key}' must be a lowercase json file name")),
            ..module.Frontend.AsEnumerable().SelectMany(frontend => frontend.ProblemsIn(module))
        ];
    }

    extension(ModuleWidget widget)
    {
        private IEnumerable<string> ProblemsIn(ModuleDescriptor module) =>
        [
            ..Require(widget.Key.StartsWith($"{module.Key}.", StringComparison.Ordinal)
                && KeyPattern.IsMatch(widget.Key[(module.Key.Length + 1)..]),
                $"Widget key '{widget.Key}' must start with '{module.Key}.' followed by a lowercase key"),
            ..Require(!string.IsNullOrWhiteSpace(widget.Name), $"Widget '{widget.Key}' needs a name"),
            ..Require(widget.Model.StartsWith(module.FrontendPath, StringComparison.Ordinal)
                && widget.Model.Length > module.FrontendPath.Length
                && WidgetModelPattern.IsMatch(widget.Model)
                && !widget.Model.Contains("//", StringComparison.Ordinal)
                && !widget.Model.EndsWith("/", StringComparison.Ordinal),
                $"Widget '{widget.Key}' must use a model under {module.FrontendPath}")
        ];
    }

    extension(ModuleGrantArea area)
    {
        private IEnumerable<string> ProblemsIn(ModuleDescriptor module) =>
        [
            ..Require(AreaPattern.IsMatch(area.Area), $"Grant area '{area.Area}' of module '{module.Key}' must start with a capital letter and contain letters and digits only"),
            ..Require(area.Actions.Count > 0, $"Grant area '{area.Area}' of module '{module.Key}' needs at least one action"),
            ..Require(
                area.Actions.All(GrantActions.All.Contains),
                $"Grant area '{area.Area}' of module '{module.Key}' may only use the actions {string.Join(", ", GrantActions.All)}")
        ];
    }

    extension(ModuleMenuItem item)
    {
        private IEnumerable<string> ProblemsIn(ModuleDescriptor module) =>
        [
            ..Require(!string.IsNullOrWhiteSpace(item.Title), $"A menu item of module '{module.Key}' needs a title"),
            ..Require(!string.IsNullOrWhiteSpace(item.Url), $"Menu item '{item.Title}' of module '{module.Key}' needs a url"),
            ..Require(item.Id != Guid.Empty, $"Menu item '{item.Title}' of module '{module.Key}' needs an id")
        ];
    }

    extension(ModuleAdministrationPage page)
    {
        private IEnumerable<string> ProblemsIn(ModuleDescriptor module) =>
        [
            ..Require(!string.IsNullOrWhiteSpace(page.Name), $"An administration page of module '{module.Key}' needs a name"),
            ..Require(page.Href.StartsWith(RoutePrefix, StringComparison.Ordinal), $"Administration page '{page.Name}' of module '{module.Key}' must link to a route starting with {RoutePrefix}"),
            ..Require(!string.IsNullOrWhiteSpace(page.Grant), $"Administration page '{page.Name}' of module '{module.Key}' needs a grant")
        ];
    }

    extension(ModuleRoute route)
    {
        private IEnumerable<string> ProblemsIn(ModuleDescriptor module) =>
        [
            ..Require(route.Path.StartsWith(RoutePrefix, StringComparison.Ordinal), $"Route '{route.Path}' of module '{module.Key}' must start with {RoutePrefix}"),
            ..Require(route.Controller.StartsWith(module.FrontendPath, StringComparison.Ordinal), $"Route '{route.Path}' of module '{module.Key}' must use a controller under {module.FrontendPath}")
        ];
    }

    extension(ModuleFrontend frontend)
    {
        private IEnumerable<string> ProblemsIn(ModuleDescriptor module) =>
            frontend.Styles.SelectMany(style => Require(
                style.StartsWith(module.FrontendPath, StringComparison.Ordinal),
                $"Style '{style}' of module '{module.Key}' must be under {module.FrontendPath}"));
    }

    extension(IReadOnlyList<ModuleDescriptor> modules)
    {
        private IEnumerable<string> CrossProblems =>
        [
            ..modules.Duplicates(module => module.Key, "Module key"),
            ..modules.SelectMany(module => module.GrantAreas).Duplicates(area => area.Area, "Grant area"),
            ..modules.SelectMany(module => module.MenuItems).Duplicates(item => item.Id, "Menu item id"),
            ..modules.SelectMany(module => module.Routes).Duplicates(route => route.Path, "Route"),
            ..modules.SelectMany(module => module.Widgets).Duplicates(widget => widget.Key, "Widget key"),
            ..modules.SelectMany(module => module.ConfigurationFiles).Duplicates(file => file, "Configuration file")
        ];
    }

    private static IEnumerable<string> Duplicates<T, TKey>(
        this IEnumerable<T> items,
        Func<T, TKey> key,
        string kind) where TKey : notnull =>
        items.GroupBy(key).Where(group => group.Count() > 1).Select(group => $"{kind} '{group.Key}' is declared more than once");

    private static IEnumerable<string> Require(bool condition, string problem) =>
        condition ? [] : [problem];
}
