namespace Behind.Core.WebApi.Layouts;

public sealed record LayoutWidget
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Type { get; init; } = "core.content";
    public string Title { get; init; } = "";
    public int Row { get; init; } = 1;
    public int Column { get; init; } = 1;
    public int RowSpan { get; init; } = 1;
    public int ColumnSpan { get; init; } = 1;
    public string Menu { get; init; } = "main";
    public string Content { get; init; } = "";
    public string Editor { get; init; } = "richtext";
    public int Limit { get; init; } = 5;
}

public sealed record LayoutSection
{
    public int Rows { get; init; } = 1;
    public int Columns { get; init; } = 1;
    public IReadOnlyList<LayoutWidget> Widgets { get; init; } = [new() { Id = "content" }];
}

public sealed record SiteLayout
{
    public string Id { get; init; } = "default";
    public string Name { get; init; } = "Default";
    public bool Deleted { get; init; }
    public IReadOnlyList<LayoutSection> Sections { get; init; } = [new()];
}

public sealed record LayoutAssignment(string Route, string Layout);

public sealed record LayoutConfiguration
{
    public long Revision { get; init; }
    public string DefaultLayout { get; init; } = "default";
    public IReadOnlyList<SiteLayout> Layouts { get; init; } = [new()];
    public IReadOnlyList<LayoutAssignment> Assignments { get; init; } = [];
}

public sealed record LayoutSaveResult(string? Error, LayoutConfiguration? Configuration);
