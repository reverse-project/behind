using System.Text.Json;
using Behind.Common.Modules;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Layouts;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi;

public sealed class LayoutConfigurationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"behind-layout-tests-{Guid.NewGuid():N}");

    private LayoutService Service() => new(
        Options.Create(new BackendConfiguration { DataDirectory = directory }),
        ModuleCatalog.Empty);

    private static LayoutConfiguration Grid(LayoutWidget widget) => new()
    {
        Layouts =
        [
            new SiteLayout
            {
                Sections =
                [
                    new LayoutSection
                    {
                        Rows = 3,
                        Columns = 3,
                        Widgets = [new LayoutWidget { Id = "content", ColumnSpan = 2, RowSpan = 2 }, widget]
                    }
                ]
            }
        ]
    };

    [Fact]
    public async Task MissingConfiguration_PreservesSingleContentDefault()
    {
        using var service = Service();
        var result = await service.Read(CancellationToken.None);
        Assert.Equal(0, result.Revision);
        Assert.Equal("default", result.DefaultLayout);
        Assert.Empty(result.Assignments);
        Assert.Equal("core.content", Assert.Single(Assert.Single(Assert.Single(result.Layouts).Sections).Widgets).Type);
        Assert.True(result.IsValid(ModuleCatalog.Empty));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Spans_CanOccupyMultipleRowsAndColumns()
    {
        Assert.True(Grid(new LayoutWidget { Id = "news", Type = "core.news", Column = 3, RowSpan = 3 }).IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(1, 2, 1, 2)]
    [InlineData(3, 3, 2, 1)]
    [InlineData(0, 3, 1, 1)]
    [InlineData(3, 3, 1, 0)]
    [InlineData(3, 3, int.MaxValue, 1)]
    public void InvalidOrOverlappingSpans_AreRejected(int row, int column, int rowSpan, int columnSpan)
    {
        var configuration = Grid(new LayoutWidget
        {
            Id = "news", Type = "core.news", Row = row, Column = column, RowSpan = rowSpan, ColumnSpan = columnSpan
        });
        Assert.False(configuration.IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData("core.content")]
    [InlineData("missing.widget")]
    public void DuplicateContentAndUninstalledWidgets_AreRejected(string type)
    {
        Assert.False(Grid(new LayoutWidget { Id = "extra", Type = type, Column = 3 }).IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public void MissingContentDuplicateIdsAndMissingAssignmentTargets_AreRejected()
    {
        var noContent = new LayoutConfiguration
        {
            Layouts = [new SiteLayout { Sections = [new LayoutSection { Widgets = [] }] }]
        };
        Assert.False(noContent.IsValid(ModuleCatalog.Empty));
        Assert.False(Grid(new LayoutWidget { Id = "content", Type = "core.news", Column = 3 }).IsValid(ModuleCatalog.Empty));
        Assert.False((new LayoutConfiguration { Assignments = [new("#/news", "missing")] }).IsValid(ModuleCatalog.Empty));
        Assert.False((new LayoutConfiguration { Assignments = [new("#/n*ews", "default")] }).IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public void NullCollectionsFromJson_AreRejected()
    {
        var configuration = JsonSerializer.Deserialize<LayoutConfiguration>(
            """{"layouts":[{"sections":[{"widgets":null}]}]}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(configuration);
        Assert.False(configuration.IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public async Task Save_PersistsSanitizedHtmlAndRejectsStaleRevision()
    {
        using var service = Service();
        var configuration = Grid(new LayoutWidget
        {
            Id = "custom", Type = "core.custom", Column = 3,
            Content = """<p onclick="alert(1)">Hello</p><script>alert(1)</script><a href="javascript:alert(1)">bad</a>"""
        });
        var saved = await service.Save(configuration, false, CancellationToken.None);
        Assert.Null(saved.Error);
        Assert.Equal(1, saved.Configuration!.Revision);
        var content = saved.Configuration.Layouts[0].Sections[0].Widgets[1].Content;
        Assert.Contains("Hello", content);
        Assert.DoesNotContain("onclick", content);
        Assert.DoesNotContain("<script", content);
        Assert.DoesNotContain("javascript:", content);
        Assert.Equal("conflict", (await service.Save(configuration, false, CancellationToken.None)).Error);

        using var reopened = Service();
        var persisted = await reopened.Read(CancellationToken.None);
        Assert.Equal(content, persisted.Layouts[0].Sections[0].Widgets[1].Content);
        Assert.Equal(1, persisted.Revision);
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task ConcurrentSaves_AllowOnlyOneMatchingRevision()
    {
        using var service = Service();
        var saves = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => service.Save(new LayoutConfiguration(), false, CancellationToken.None)));
        Assert.Single(saves, result => result.Error is null);
        Assert.Equal(9, saves.Count(result => result.Error == "conflict"));
    }

    [Fact]
    public async Task DeletedLayouts_AreRetainedForNonDevelopersAndRestorableOnlyByDevelopers()
    {
        using var service = Service();
        var configuration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout(), new SiteLayout { Id = "sidebar", Name = "Sidebar" }]
        };
        var first = (await service.Save(configuration, false, CancellationToken.None)).Configuration!;
        var deleted = first with { Layouts = [first.Layouts[0], first.Layouts[1] with { Deleted = true }] };
        var second = (await service.Save(deleted, false, CancellationToken.None)).Configuration!;
        var activeOnly = second with { Layouts = [second.Layouts[0]] };
        var third = (await service.Save(activeOnly, false, CancellationToken.None)).Configuration!;
        Assert.Equal(2, third.Layouts.Count);
        Assert.True(third.Layouts[1].Deleted);
        var restore = third with { Layouts = [third.Layouts[0], third.Layouts[1] with { Deleted = false }] };
        Assert.Equal("forbidden", (await service.Save(restore, false, CancellationToken.None)).Error);
        var restored = await service.Save(restore, true, CancellationToken.None);
        Assert.Null(restored.Error);
        Assert.False(restored.Configuration!.Layouts[1].Deleted);
        var deletedAgain = restored.Configuration with
        {
            Layouts = [restored.Configuration.Layouts[0], restored.Configuration.Layouts[1] with { Deleted = true }]
        };
        var fourth = (await service.Save(deletedAgain, true, CancellationToken.None)).Configuration!;
        var removed = await service.Save(fourth with { Layouts = [fourth.Layouts[0]] }, true, CancellationToken.None);
        Assert.Null(removed.Error);
        Assert.Single(removed.Configuration!.Layouts);
    }

    [Fact]
    public async Task CorruptConfiguration_IsAnExplicitErrorAndIsNeverOverwritten()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "layouts.json");
        await File.WriteAllTextAsync(path, """{"layouts":[]}""");
        using var service = Service();
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Read(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Save(new LayoutConfiguration(), false, CancellationToken.None));
        Assert.Equal("""{"layouts":[]}""", await File.ReadAllTextAsync(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
