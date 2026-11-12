using System.Text.RegularExpressions;
using Behind.Common.Modules;
using Behind.Common.WebApi;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Layouts;

public static partial class LayoutValidationExtensions
{
    private static readonly IReadOnlySet<string> CoreWidgets = new HashSet<string>(StringComparer.Ordinal)
    {
        "core.content", "core.news", "core.articles", "core.downloads",
        "core.menu-horizontal", "core.menu-vertical", "core.custom", "core.chat", MessageWidget,
        "core.header", "core.footer"
    };

    private const string MessageWidget = "core.message";

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\A(?:[-_a-zA-Z][-_a-zA-Z0-9]*(?: +[-_a-zA-Z][-_a-zA-Z0-9]*)*)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex CssClasses();

    extension(string? value)
    {
        private bool IsValidCssClass => value is { Length: <= 500 } && CssClasses().IsMatch(value);
    }

    extension(LayoutWidget widget)
    {
        private bool HasValidMessage =>
            widget.Type == MessageWidget
                ? Guid.TryParse(widget.Message, out _)
                : widget.Message is null or { Length: <= 64 };
    }

    extension(LayoutConfiguration configuration)
    {
        public bool IsValid(ModuleCatalog modules, bool requireInstalledWidgets = true)
        {
            if (configuration.SchemaVersion is < 0 or > 1 || configuration.Revision < 0 || configuration.Layouts is null
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
                    || layout.Locale is { Length: 0 or > 100 }
                    || layout.Sections is null || layout.Sections.Count < 1
                    || layout.Badges is null || layout.Badges.Count > 20
                    || layout.Sections.Count > (configuration.SchemaVersion == 0 ? 20 : 22))
                {
                    return false;
                }

                var widgetIds = new HashSet<string>(StringComparer.Ordinal);
                var contentCount = 0;
                var badgeIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (var badge in layout.Badges)
                {
                    if (badge is null || string.IsNullOrWhiteSpace(badge.Id) || badge.Id.Length > 64
                        || !badgeIds.Add(badge.Id) || badge.Text is null || badge.Text.Length > 200
                        || badge.Image is null || badge.Image.Length > 200
                        || string.IsNullOrWhiteSpace(badge.Text)
                        || badge.Link is null || !badge.Link.IsSafeCarouselLink
                        || badge.Link.Any(character => char.IsControl(character) || character == '\\')
                        || badge.Position is not ("top-left" or "top-right" or "bottom-left" or "bottom-right")
                        || badge.Width is < 40 or > 600
                        || badge.Image.Length > 0 && (!badge.Image.StartsWith($"{VfsTypes.LayoutImageUrl}/", StringComparison.Ordinal)
                            || !Guid.TryParseExact(badge.Image[(VfsTypes.LayoutImageUrl.Length + 1)..], "D", out var image)
                            || image == Guid.Empty))
                    {
                        return false;
                    }
                }

                foreach (var section in layout.Sections)
                {
                    if (section is null || section.Rows is < 1 or > 12 || section.Columns is < 1 or > 12
                        || section.Width is not ("site" or "full")
                        || !section.CssClass.IsValidCssClass
                        || section.Overlap is < 0 or > 2000 || section.Layer is < -100 or > 100
                        || section.Widgets is null || section.Widgets.Count > 100)
                    {
                        return false;
                    }

                    foreach (var widget in section.Widgets)
                    {
                        if (widget is null || string.IsNullOrWhiteSpace(widget.Id) || widget.Id.Length > 64
                            || !widgetIds.Add(widget.Id) || string.IsNullOrEmpty(widget.Type) || widget.Type.Length > 100
                            || requireInstalledWidgets && !supported.Contains(widget.Type)
                            || widget.Title is null || widget.Title.Length > 200 || widget.Menu is null || widget.Menu.Length > 200
                            || !widget.CssClass.IsValidCssClass
                            || widget.Content is null || widget.Content.Length > 100_000
                            || widget.Editor is not ("html" or "richtext") || widget.Limit is < 1 or > 50
                            || widget.Layer is < -100 or > 100
                            || widget.HamburgerPosition is not ("default" or "left" or "right")
                            || widget.Type == "core.locale-switch" && widget.LocaleSwitchView is not ("view" or "flag-only" or "flag-and-text" or "text-only")
                            || !widget.HasValidMessage
                            || widget.Row < 1 || widget.Column < 1 || widget.RowSpan is < 1 or > 12
                            || widget.ColumnSpan is < 1 or > 12 || widget.Row > section.Rows - widget.RowSpan + 1
                            || widget.Column > section.Columns - widget.ColumnSpan + 1)
                        {
                            return false;
                        }

                        contentCount += widget.Type == "core.content" ? 1 : 0;

                    }
                }

                if (contentCount != 1)
                {
                    return false;
                }
            }

            var active = configuration.Layouts.Where(layout => !layout.Deleted)
                .ToDictionary(layout => layout.Id, layout => layout.Locale, StringComparer.Ordinal);
            var routes = new HashSet<(string Route, string? Locale)>();

            return active.ContainsKey(configuration.DefaultLayout) && configuration.Assignments.All(assignment =>
                assignment is not null && assignment.Route is { Length: > 1 and <= 500 }
                && assignment.Route.StartsWith("#/", StringComparison.Ordinal)
                && !assignment.Route[..^1].Contains('*')
                && assignment.Layout is not null && active.TryGetValue(assignment.Layout, out var assignmentLocale)
                && routes.Add((assignment.Route, assignmentLocale)));
        }

        public LayoutConfiguration Sanitize() => configuration with
        {
            Layouts = configuration.Layouts.Select(layout => layout with
            {
                Sections = layout.Sections.Select(section => section with
                {
                    Widgets = section.Widgets.Select(widget => widget with
                    {
                        Content = widget.Content.SanitizeRichText(),
                        Message = widget.Message ?? ""
                    }).ToArray()
                }).ToArray()
            }).ToArray()
        };
    }
}
