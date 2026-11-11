using System.Text.RegularExpressions;
using Behind.Common.Modules;
using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Layouts;

public static partial class LayoutValidationExtensions
{
    private static readonly IReadOnlySet<string> CoreWidgets = new HashSet<string>(StringComparer.Ordinal)
    {
        "core.content", "core.news", "core.articles", "core.downloads",
        "core.menu-horizontal", "core.menu-vertical", "core.custom", "core.chat"
    };

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    extension(LayoutConfiguration configuration)
    {
        public bool IsValid(ModuleCatalog modules, bool requireInstalledWidgets = true)
        {
            if (configuration.Revision < 0 || configuration.Layouts is null
                || configuration.Layouts.Count is < 1 or > 100 || configuration.Assignments is null
                || configuration.Assignments.Count > 500 || string.IsNullOrEmpty(configuration.DefaultLayout))
            {
                return false;
            }

            var supported = CoreWidgets.Concat(modules.Modules.SelectMany(module => module.Widgets).Select(widget => widget.Key))
                .ToHashSet(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (var layout in configuration.Layouts)
            {
                if (layout is null || string.IsNullOrEmpty(layout.Id) || !Identifier().IsMatch(layout.Id)
                    || !ids.Add(layout.Id) || string.IsNullOrWhiteSpace(layout.Name) || layout.Name.Length > 200
                    || layout.Sections is null || layout.Sections.Count is < 1 or > 20)
                {
                    return false;
                }

                var widgetIds = new HashSet<string>(StringComparer.Ordinal);
                var contentCount = 0;

                foreach (var section in layout.Sections)
                {
                    if (section is null || section.Rows is < 1 or > 12 || section.Columns is < 1 or > 12
                        || section.Widgets is null || section.Widgets.Count > 100)
                    {
                        return false;
                    }

                    var occupied = new HashSet<(int Row, int Column)>();

                    foreach (var widget in section.Widgets)
                    {
                        if (widget is null || string.IsNullOrWhiteSpace(widget.Id) || widget.Id.Length > 64
                            || !widgetIds.Add(widget.Id) || string.IsNullOrEmpty(widget.Type) || widget.Type.Length > 100
                            || requireInstalledWidgets && !supported.Contains(widget.Type)
                            || widget.Title is null || widget.Title.Length > 200 || widget.Menu is null || widget.Menu.Length > 200
                            || widget.Content is null || widget.Content.Length > 100_000
                            || widget.Editor is not ("html" or "richtext") || widget.Limit is < 1 or > 50
                            || widget.Row < 1 || widget.Column < 1 || widget.RowSpan is < 1 or > 12
                            || widget.ColumnSpan is < 1 or > 12 || widget.Row > section.Rows - widget.RowSpan + 1
                            || widget.Column > section.Columns - widget.ColumnSpan + 1)
                        {
                            return false;
                        }

                        contentCount += widget.Type == "core.content" ? 1 : 0;

                        for (var row = widget.Row; row < widget.Row + widget.RowSpan; row++)
                        {
                            for (var column = widget.Column; column < widget.Column + widget.ColumnSpan; column++)
                            {
                                if (!occupied.Add((row, column)))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }

                if (contentCount != 1)
                {
                    return false;
                }
            }

            var active = configuration.Layouts.Where(layout => !layout.Deleted).Select(layout => layout.Id)
                .ToHashSet(StringComparer.Ordinal);
            var routes = new HashSet<string>(StringComparer.Ordinal);

            return active.Contains(configuration.DefaultLayout) && configuration.Assignments.All(assignment =>
                assignment is not null && assignment.Route is { Length: > 1 and <= 500 }
                && assignment.Route.StartsWith("#/", StringComparison.Ordinal)
                && !assignment.Route[..^1].Contains('*') && routes.Add(assignment.Route)
                && assignment.Layout is not null && active.Contains(assignment.Layout));
        }

        public LayoutConfiguration Sanitize() => configuration with
        {
            Layouts = configuration.Layouts.Select(layout => layout with
            {
                Sections = layout.Sections.Select(section => section with
                {
                    Widgets = section.Widgets.Select(widget => widget with
                    {
                        Content = widget.Content.SanitizeRichText()
                    }).ToArray()
                }).ToArray()
            }).ToArray()
        };
    }
}
