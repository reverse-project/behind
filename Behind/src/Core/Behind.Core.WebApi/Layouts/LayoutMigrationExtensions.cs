namespace Behind.Core.WebApi.Layouts;

public static class LayoutMigrationExtensions
{
    extension(LayoutConfiguration configuration)
    {
        public LayoutConfiguration UpgradePageLayouts()
        {
            if (configuration.SchemaVersion != 0)
            {
                return configuration;
            }

            return configuration with
            {
                SchemaVersion = 1,
                Layouts = configuration.Layouts.Select(layout =>
                {
                    var widgets = layout.Sections.SelectMany(section => section.Widgets).ToArray();
                    var ids = widgets.Select(widget => widget.Id).ToHashSet(StringComparer.Ordinal);
                    var headerId = UniqueId("layout-header", ids);
                    var footerId = UniqueId("layout-footer", ids);
                    LayoutSection[] header = widgets.Any(widget => widget.Type == "core.header") ? [] :
                    [
                        new()
                        {
                            Width = "full",
                            Widgets = [new LayoutWidget { Id = headerId, Type = "core.header" }]
                        }
                    ];
                    LayoutSection[] footer = widgets.Any(widget => widget.Type == "core.footer") ? [] :
                    [
                        new()
                        {
                            Width = "full",
                            Widgets = [new LayoutWidget { Id = footerId, Type = "core.footer" }]
                        }
                    ];

                    return layout with
                    {
                        Sections =
                        [
                            ..header,
                            ..layout.Sections.Select((section, index) => index == 0 ? section with { Overlap = 0 } : section),
                            ..footer
                        ]
                    };
                }).ToArray()
            };
        }
    }

    private static string UniqueId(string prefix, HashSet<string> ids)
    {
        var id = prefix;

        for (var suffix = 1; !ids.Add(id); suffix++)
        {
            id = $"{prefix}-{suffix}";
        }

        return id;
    }
}
