namespace Behind.Common.Modules;

public static class ModuleVisibilityExtensions
{
    extension(ModuleAvailability availability)
    {
        public bool IsRouteEnabled(ModuleCatalog catalog, string url)
        {
            var hash = url.IndexOf("#/", StringComparison.Ordinal);
            if (hash < 0)
            {
                return true;
            }

            var segments = url[(hash + 2)..].Split('?', 2)[0].TrimEnd('/').Split('/');
            return !catalog.Modules.Where(module => !availability.IsEnabled(module.Key))
                .SelectMany(module => module.Routes)
                .Any(route => Matches(route.Path[2..].TrimEnd('/').Split('/'), segments, 0)
                    || Matches(route.Path[2..].TrimEnd('/').Split('/'), segments, 1));
        }

        public bool IsWidgetEnabled(ModuleCatalog catalog, string key) =>
            !catalog.Modules.Any(module => !availability.IsEnabled(module.Key)
                && (module.Widgets.Any(widget => widget.Key == key)
                    || key.StartsWith($"{module.Key}.", StringComparison.Ordinal)));
    }

    private static bool Matches(string[] pattern, string[] segments, int offset) =>
        pattern.Length == segments.Length - offset
        && pattern.Select((segment, index) =>
            segment.StartsWith(':') || segment == segments[index + offset]).All(matches => matches);
}
