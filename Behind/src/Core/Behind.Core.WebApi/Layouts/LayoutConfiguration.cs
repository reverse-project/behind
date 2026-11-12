namespace Behind.Core.WebApi.Layouts;

public sealed record LayoutWidget
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Type { get; init; } = "core.content";
    public string Title { get; init; } = "";
    public string CssClass { get; init; } = "";
    public bool Borderless { get; init; }
    public bool NoMargin { get; init; }
    public bool NoPadding { get; init; }
    public string HamburgerPosition { get; init; } = "default";
    public int Row { get; init; } = 1;
    public int Column { get; init; } = 1;
    public int RowSpan { get; init; } = 1;
    public int ColumnSpan { get; init; } = 1;
    public int Layer { get; init; }
    public string Menu { get; init; } = "main";
    public string Content { get; init; } = "";
    public string Editor { get; init; } = "richtext";
    public int Limit { get; init; } = 5;
    public string Message { get; init; } = "";
}

public sealed record LayoutSection
{
    public int Rows { get; init; } = 1;
    public int Columns { get; init; } = 1;
    public string Width { get; init; } = "site";
    public string CssClass { get; init; } = "";
    public int Overlap { get; init; }
    public int Layer { get; init; }
    public IReadOnlyList<LayoutWidget> Widgets { get; init; } = [new() { Id = "content" }];
}

public sealed record LayoutBadge
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Text { get; init; } = "";
    public string Image { get; init; } = "";
    public string Link { get; init; } = "";
    public string Position { get; init; } = "top-right";
    public int Width { get; init; } = 180;
}

public sealed record SiteLayout
{
    public string Id { get; init; } = "default";
    public string Name { get; init; } = "Default";
    public bool Deleted { get; init; }
    public IReadOnlyList<LayoutBadge> Badges { get; init; } = [];
    public IReadOnlyList<LayoutSection> Sections { get; init; } = [new()];
}

public sealed record LayoutAssignment(string Route, string Layout);

public sealed record LayoutConfiguration
{
    public static readonly Guid ImagesKey = new("80aa569b-a8c7-4174-9ed8-15ee7c4c0ce0");

    public long Revision { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public string DefaultLayout { get; init; } = "default";
    public IReadOnlyList<SiteLayout> Layouts { get; init; } =
    [
        new()
        {
            Sections =
            [
                new() { Width = "full", Widgets = [new() { Id = "header", Type = "core.header" }] },
                new(),
                new() { Width = "full", Widgets = [new() { Id = "footer", Type = "core.footer" }] }
            ]
        }
    ];
    public IReadOnlyList<LayoutAssignment> Assignments { get; init; } = [];
}

public sealed record LayoutSaveResult(string? Error, LayoutConfiguration? Configuration);
