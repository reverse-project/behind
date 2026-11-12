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
    public async Task MissingConfiguration_UsesConfigurableHeaderContentAndFooter()
    {
        using var service = Service();
        var result = await service.Read(CancellationToken.None);
        Assert.Equal(0, result.Revision);
        Assert.Equal("default", result.DefaultLayout);
        Assert.Empty(result.Assignments);
        Assert.Equal(1, result.SchemaVersion);
        var sections = Assert.Single(result.Layouts).Sections;
        Assert.Equal(["core.header", "core.content", "core.footer"],
            sections.Select(section => Assert.Single(section.Widgets).Type));
        Assert.Equal(["full", "site", "full"], sections.Select(section => section.Width));
        Assert.True(result.IsValid(ModuleCatalog.Empty));
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("site-banner")]
    [InlineData("_private theme-dark rounded-2")]
    [InlineData("-custom two_classes")]
    public void SectionAndWidgetCssClasses_AcceptIdentifiers(string cssClass)
    {
        var configuration = Grid(new LayoutWidget { Id = "custom", Type = "core.custom", Column = 3, CssClass = cssClass });
        configuration = configuration with
        {
            Layouts = [configuration.Layouts[0] with
            {
                Sections = [configuration.Layouts[0].Sections[0] with { CssClass = cssClass }]
            }]
        };
        Assert.True(configuration.IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("two\nclasses")]
    [InlineData("valid\n")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("9invalid")]
    [InlineData("x\" onclick=\"alert(1)")]
    [InlineData("<script>")]
    [InlineData("theme:invalid")]
    public void InvalidSectionAndWidgetCssClasses_AreRejected(string? cssClass)
    {
        var configuration = Grid(new LayoutWidget { Id = "custom", Type = "core.custom", Column = 3, CssClass = cssClass! });
        Assert.False(configuration.IsValid(ModuleCatalog.Empty));
        var sectionConfiguration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout { Sections = [new LayoutSection { CssClass = cssClass! }] }]
        };
        Assert.False(sectionConfiguration.IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void CssClassLength_IsBounded(int length, bool valid)
    {
        Assert.Equal(valid, Grid(new LayoutWidget
        {
            Id = "custom", Type = "core.custom", Column = 3, CssClass = new string('a', length)
        }).IsValid(ModuleCatalog.Empty));
        Assert.Equal(valid, (new LayoutConfiguration
        {
            Layouts = [new SiteLayout { Sections = [new LayoutSection { CssClass = new string('a', length) }] }]
        }).IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public async Task CssClassesAndHeaderlessPage_PersistWithoutReintroducingChrome()
    {
        using var service = Service();
        var configuration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout { Sections = [new LayoutSection
            {
                CssClass = "page-body",
                Widgets = [new LayoutWidget { Id = "content", CssClass = "content-area" }]
            }] }]
        };
        var saved = await service.Save(configuration, false, CancellationToken.None);
        Assert.Null(saved.Error);
        using var reopened = Service();
        var loaded = await reopened.Read(CancellationToken.None);
        Assert.Equal(1, loaded.SchemaVersion);
        var section = Assert.Single(Assert.Single(loaded.Layouts).Sections);
        Assert.Equal("page-body", section.CssClass);
        Assert.Equal("content-area", Assert.Single(section.Widgets).CssClass);
        Assert.Equal("core.content", section.Widgets[0].Type);
    }

    [Fact]
    public async Task CaseInsensitiveSchemaProperty_PreservesHeaderlessPage()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "layouts.json"),
            """{"SchemaVersion":1,"layouts":[{"id":"default","name":"Minimal","sections":[{"widgets":[{"id":"content"}]}]}]}""");
        using var service = Service();
        var configuration = await service.Read(CancellationToken.None);
        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Single(Assert.Single(configuration.Layouts).Sections);
    }

    [Fact]
    public async Task HeaderHamburgerPosition_DefaultsToSiteAndPersists()
    {
        Assert.Equal("default", new LayoutWidget().HamburgerPosition);
        using var service = Service();
        var saved = await service.Save(Grid(new LayoutWidget
        {
            Id = "header", Type = "core.header", Column = 3, HamburgerPosition = "left"
        }), false, CancellationToken.None);
        Assert.Null(saved.Error);
        var loaded = await service.Read(CancellationToken.None);
        Assert.Equal("left", loaded.Layouts[0].Sections[0].Widgets[1].HamburgerPosition);
    }

    [Theory]
    [InlineData("default", true)]
    [InlineData("left", true)]
    [InlineData("right", true)]
    [InlineData("center", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HeaderHamburgerPosition_IsValidated(string? position, bool valid)
    {
        Assert.Equal(valid, Grid(new LayoutWidget
        {
            Id = "header", Type = "core.header", Column = 3, HamburgerPosition = position!
        }).IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public async Task CellAppearanceOptions_DefaultOffAndPersistIndependently()
    {
        var old = JsonSerializer.Deserialize<LayoutWidget>("""{"id":"legacy"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.False(old.Borderless);
        Assert.False(old.NoMargin);
        Assert.False(old.NoPadding);
        using var service = Service();
        var configuration = Grid(new LayoutWidget
        {
            Id = "custom", Type = "core.custom", Column = 3,
            Borderless = true, NoMargin = true, NoPadding = true
        });
        var saved = await service.Save(configuration, false, CancellationToken.None);
        Assert.Null(saved.Error);
        using var reopened = Service();
        var loaded = await reopened.Read(CancellationToken.None);
        var widgets = loaded.Layouts[0].Sections[0].Widgets;
        Assert.True(widgets[1].Borderless);
        Assert.True(widgets[1].NoMargin);
        Assert.True(widgets[1].NoPadding);
        Assert.False(widgets[0].Borderless);
        Assert.False(widgets[0].NoMargin);
        Assert.False(widgets[0].NoPadding);
        var updated = loaded with
        {
            Layouts = [loaded.Layouts[0] with { Sections = [loaded.Layouts[0].Sections[0] with
            {
                Widgets = [widgets[0], widgets[1] with { Borderless = false, NoMargin = false, NoPadding = false }]
            }] }]
        };
        Assert.Null((await reopened.Save(updated, false, CancellationToken.None)).Error);
        var reset = (await reopened.Read(CancellationToken.None)).Layouts[0].Sections[0].Widgets[1];
        Assert.False(reset.Borderless);
        Assert.False(reset.NoMargin);
        Assert.False(reset.NoPadding);
    }

    [Fact]
    public void HeaderAndFooterWidgets_AreOptionalAndMayBeRepeated()
    {
        var configuration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout { Sections = [new LayoutSection
            {
                Widgets =
                [
                    new LayoutWidget { Id = "content" },
                    new LayoutWidget { Id = "header-one", Type = "core.header" },
                    new LayoutWidget { Id = "header-two", Type = "core.header" },
                    new LayoutWidget { Id = "footer-one", Type = "core.footer" },
                    new LayoutWidget { Id = "footer-two", Type = "core.footer" }
                ]
            }] }]
        };
        Assert.True(configuration.IsValid(ModuleCatalog.Empty));
        Assert.True((configuration with { Layouts = [new SiteLayout()] }).IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyFile_IsUpgradedOnceWithoutOverwritingUntilSave(bool explicitVersion)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "layouts.json");
        var legacy = $$"""
            { {{(explicitVersion ? "\"schemaVersion\": 0," : "")}}
              "revision": 7, "defaultLayout": "default",
              "layouts": [
                { "id": "default", "name": "Legacy", "sections": [
                  { "overlap": 99, "widgets": [
                    { "id": "layout-header", "type": "core.content" },
                    { "id": "layout-footer", "type": "core.news" }
                  ] }
                ] },
                { "id": "deleted", "name": "Deleted", "deleted": true }
              ],
              "assignments": [{ "route": "#/news", "layout": "default" }]
            }
            """;
        await File.WriteAllTextAsync(path, legacy);
        using var service = Service();
        var migrated = await service.Read(CancellationToken.None);
        Assert.Equal(1, migrated.SchemaVersion);
        Assert.Equal(7, migrated.Revision);
        Assert.Equal(legacy, await File.ReadAllTextAsync(path));
        Assert.True(migrated.Layouts[1].Deleted);
        Assert.Equal(3, migrated.Layouts[1].Sections.Count);
        var sections = migrated.Layouts[0].Sections;
        Assert.Equal(3, sections.Count);
        Assert.Equal("core.header", Assert.Single(sections[0].Widgets).Type);
        Assert.Equal("layout-header-1", sections[0].Widgets[0].Id);
        Assert.Equal("layout-header", sections[1].Widgets[0].Id);
        Assert.Equal(0, sections[1].Overlap);
        Assert.Equal("core.footer", Assert.Single(sections[2].Widgets).Type);
        Assert.Equal("layout-footer-1", sections[2].Widgets[0].Id);
        Assert.Single(migrated.Assignments);
        Assert.True(migrated.IsValid(ModuleCatalog.Empty));
        Assert.Null((await service.Save(migrated, true, CancellationToken.None)).Error);
        using var reopened = Service();
        var loaded = await reopened.Read(CancellationToken.None);
        Assert.Equal(3, loaded.Layouts[0].Sections.Count);
        Assert.Equal(8, loaded.Revision);
    }

    [Fact]
    public void LegacyMaximumSections_HaveRoomForChrome()
    {
        var legacy = new LayoutConfiguration
        {
            SchemaVersion = 0,
            Layouts = [new SiteLayout
            {
                Sections = [new(), ..Enumerable.Range(1, 19).Select(_ => new LayoutSection { Widgets = [] })]
            }]
        };
        Assert.True(legacy.IsValid(ModuleCatalog.Empty));
        var upgraded = legacy.UpgradePageLayouts();
        Assert.Equal(22, upgraded.Layouts[0].Sections.Count);
        Assert.True(upgraded.IsValid(ModuleCatalog.Empty));
        Assert.Same(upgraded, upgraded.UpgradePageLayouts());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task UnsupportedSchema_IsRejectedAndNotOverwritten(int version)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "layouts.json");
        var json = JsonSerializer.Serialize(new LayoutConfiguration { SchemaVersion = version },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await File.WriteAllTextAsync(path, json);
        using var service = Service();
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Read(CancellationToken.None));
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void Spans_CanOccupyMultipleRowsAndColumns()
    {
        Assert.True(Grid(new LayoutWidget { Id = "news", Type = "core.news", Column = 3, RowSpan = 3 }).IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData("18fdcc69-269d-4c1a-9123-56f83cd36d50", true)]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    [InlineData(null, false)]
    public void MessageWidgets_RequireAValidMessageId(string? message, bool valid)
    {
        Assert.Equal(valid, Grid(new LayoutWidget
        {
            Id = "banner", Type = "core.message", Column = 3, Message = message!
        }).IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public void MultipleMessageWidgets_AreAllowed()
    {
        var configuration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout
            {
                Sections = [new LayoutSection
                {
                    Columns = 3,
                    Widgets =
                    [
                        new LayoutWidget { Id = "content" },
                        new LayoutWidget { Id = "first", Type = "core.message", Column = 2, Message = Guid.NewGuid().ToString() },
                        new LayoutWidget { Id = "second", Type = "core.message", Column = 3, Message = Guid.NewGuid().ToString() }
                    ]
                }]
            }]
        };
        Assert.True(configuration.IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData(3, 3, 2, 1)]
    [InlineData(0, 3, 1, 1)]
    [InlineData(3, 3, 1, 0)]
    [InlineData(3, 3, int.MaxValue, 1)]
    public void InvalidSpans_AreRejected(int row, int column, int rowSpan, int columnSpan)
    {
        var configuration = Grid(new LayoutWidget
        {
            Id = "news", Type = "core.news", Row = row, Column = column, RowSpan = rowSpan, ColumnSpan = columnSpan
        });
        Assert.False(configuration.IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(1, 2, 1, 2)]
    public void OverlappingWidgets_AreValid(int row, int column, int rowSpan, int columnSpan)
    {
        Assert.True(Grid(new LayoutWidget
        {
            Id = "news", Type = "core.news", Row = row, Column = column, RowSpan = rowSpan, ColumnSpan = columnSpan
        }).IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData(-1, 0, 0, false)]
    [InlineData(2001, 0, 0, false)]
    [InlineData(0, -101, 0, false)]
    [InlineData(0, 101, 0, false)]
    [InlineData(0, 0, -101, false)]
    [InlineData(0, 0, 101, false)]
    [InlineData(2000, -100, 100, true)]
    public void OverlapAndLayers_AreBounded(int overlap, int sectionLayer, int widgetLayer, bool valid)
    {
        var configuration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout { Sections = [new LayoutSection
            {
                Overlap = overlap, Layer = sectionLayer,
                Widgets = [new LayoutWidget { Layer = widgetLayer }]
            }] }]
        };
        Assert.Equal(valid, configuration.IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public async Task OverlappingWidgetsAndSections_SurviveSaveAndReload()
    {
        using var service = Service();
        var configuration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout { Sections =
            [
                new LayoutSection { Widgets = [new LayoutWidget { Id = "content" }, new LayoutWidget { Id = "news", Type = "core.news", Layer = 3 }] },
                new LayoutSection { Overlap = 42, Layer = -2, Widgets = [] }
            ] }]
        };
        var saved = await service.Save(configuration, false, CancellationToken.None);
        Assert.Null(saved.Error);
        using var reopened = Service();
        var sections = (await reopened.Read(CancellationToken.None)).Layouts[0].Sections;
        Assert.Equal(3, sections[0].Widgets[1].Layer);
        Assert.Equal(42, sections[1].Overlap);
        Assert.Equal(-2, sections[1].Layer);
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

    [Theory]
    [InlineData("site", true)]
    [InlineData("full", true)]
    [InlineData("wide", false)]
    [InlineData("Full", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SectionWidth_AcceptsOnlySiteAndFull(string? width, bool valid)
    {
        var configuration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout { Sections = [new LayoutSection { Width = width! }] }]
        };
        Assert.Equal(valid, configuration.IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public void SectionWidth_DefaultsToSiteAndOlderJsonWithoutWidthLoadsAsSite()
    {
        Assert.Equal("site", new LayoutSection().Width);
        var configuration = JsonSerializer.Deserialize<LayoutConfiguration>(
            """{"layouts":[{"id":"default","name":"D","sections":[{"rows":1,"columns":1,"widgets":[{"id":"content"}]}]}]}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(configuration);
        var section = Assert.Single(Assert.Single(configuration.Layouts).Sections);
        Assert.Equal("site", section.Width);
        Assert.Equal(0, section.Overlap);
        Assert.Equal(0, section.Layer);
        Assert.Equal(0, Assert.Single(section.Widgets).Layer);
        Assert.True(configuration.IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public void SectionWidth_SerializesAsCamelCaseProperty()
    {
        var json = JsonSerializer.Serialize(new LayoutSection { Width = "full" }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"width\":\"full\"", json);
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
    public async Task CustomWidget_SaveAndReloadPreserveSafeImageAlignment()
    {
        using var service = Service();
        var image = Guid.NewGuid();
        var configuration = Grid(new LayoutWidget
        {
            Id = "custom", Type = "core.custom", Column = 3,
            Content = $"""<p class="ql-align-right"><img class="ql-image-center" width="120" src="/layouts/images/{image}" onerror="alert(1)"></p>"""
        });
        var saved = await service.Save(configuration, false, CancellationToken.None);
        Assert.Null(saved.Error);
        using var reopened = Service();
        var content = (await reopened.Read(CancellationToken.None)).Layouts[0].Sections[0].Widgets[1].Content;
        Assert.Contains("ql-align-right", content);
        Assert.Contains("ql-image-center", content);
        Assert.Contains($"/layouts/images/{image}", content);
        Assert.Contains("width=\"120\"", content);
        Assert.DoesNotContain("onerror", content);
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
