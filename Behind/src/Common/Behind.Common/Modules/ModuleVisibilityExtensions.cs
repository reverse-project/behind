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
            var disabledRoutes = catalog.Modules.Where(module => !availability.IsEnabled(module.Key))
                .SelectMany(module => module.Routes)
                .Select(route => route.Path[2..].TrimEnd('/').Split('/'))
                .ToArray();
            return !disabledRoutes.Any(route => Matches(route, segments, 0)
                || IsContentLocale(segments[0]) && Matches(route, segments, 1));
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

    private static bool IsContentLocale(string segment) =>
        segment.Length >= 5
        && segment[2] == '-'
        && segment[..2].All(char.IsLetter)
        && segment[3..].All(character => char.IsLetterOrDigit(character));
}
