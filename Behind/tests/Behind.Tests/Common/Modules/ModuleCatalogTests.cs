namespace Behind.Tests.Common.Modules;

public class ModuleCatalogTests
{
    [Fact]
    public void ContentRestrictions_AggregateExplicitModuleEntitiesAndAdditionalParentProperties()
    {
        var descriptor = new ModuleDescriptor("content", "Content")
        {
            Database = new ModuleDatabase(typeof(ModuleCatalogTests).Assembly)
            {
                ContentRestrictedEntities = [typeof(News), typeof(News)],
                ContentRestrictionParentProperties = ["CustomParentId", "CustomParentId"]
            }
        };
        var catalog = ModuleCatalog.Create([descriptor]).GetOrElse(ModuleCatalog.Empty);

        Assert.Equal([typeof(News)], catalog.ContentRestrictedEntities);
        Assert.Equal(["CustomParentId"], catalog.ContentRestrictionParentProperties);
    }

    private static ModuleDescriptor Module(string key = "blog") => new(key, "Blog");

    private static IReadOnlyList<string> Problems(params ModuleDescriptor[] modules) =>
        ModuleCatalog.Create(modules).Match(
            errors => errors.Select(error => error.ToString()).ToList(),
            _ => []);

    [Fact]
    public void Create_AcceptsAValidModule()
    {
        var catalog = ModuleCatalog.Create([NotesModule.Descriptor]);

        Assert.True(catalog.IsValid);
    }

    [Fact]
    public void Create_AcceptsNoModules()
    {
        Assert.Equal(ModuleCatalog.Empty.Modules, ModuleCatalog.Create([]).GetOrElse(ModuleCatalog.Empty).Modules);
    }

    [Theory]
    [InlineData("Blog")]
    [InlineData("1blog")]
    [InlineData("bl og")]
    [InlineData("")]
    public void Create_RejectsAnInvalidKey(string key)
    {
        Assert.Contains(Problems(Module(key)), problem => problem.Contains("must be lowercase"));
    }

    [Fact]
    public void Create_RejectsAModuleWithoutName()
    {
        Assert.Contains(Problems(new ModuleDescriptor("blog", " ")), problem => problem.Contains("needs a name"));
    }

    [Fact]
    public void Create_RejectsDuplicateKeys()
    {
        Assert.Contains(Problems(Module(), Module()), problem => problem.Contains("Module key 'blog'"));
    }

    [Theory]
    [InlineData("posts")]
    [InlineData("Po sts")]
    public void Create_RejectsAnInvalidGrantArea(string area)
    {
        var module = Module() with { GrantAreas = [new ModuleGrantArea(area, ModuleAudience.Everyone)] };

        Assert.Contains(Problems(module), problem => problem.Contains("must start with a capital letter"));
    }

    [Fact]
    public void Create_RejectsGrantAreasWithoutOrWithUnknownActions()
    {
        var empty = Module() with { GrantAreas = [new ModuleGrantArea("Posts", ModuleAudience.Everyone, [])] };
        var unknown = Module() with { GrantAreas = [new ModuleGrantArea("Posts", ModuleAudience.Everyone, ["Publish"])] };

        Assert.Contains(Problems(empty), problem => problem.Contains("at least one action"));
        Assert.Contains(Problems(unknown), problem => problem.Contains("may only use the actions"));
    }

    [Fact]
    public void Create_RejectsDuplicateGrantAreasMenuItemsAndRoutesAcrossModules()
    {
        var id = Guid.NewGuid();
        var first = Module("one") with
        {
            GrantAreas = [new ModuleGrantArea("Posts", ModuleAudience.Everyone)],
            MenuItems = [new ModuleMenuItem(id, "Posts", "#/posts", 1)],
            Routes = [new ModuleRoute("#/posts", "modules/one/controllers/posts")]
        };
        var second = Module("two") with
        {
            GrantAreas = [new ModuleGrantArea("Posts", ModuleAudience.Everyone)],
            MenuItems = [new ModuleMenuItem(id, "Posts", "#/posts", 1)],
            Routes = [new ModuleRoute("#/posts", "modules/two/controllers/posts")]
        };

        var problems = Problems(first, second);

        Assert.Contains(problems, problem => problem.Contains("Grant area 'Posts'"));
        Assert.Contains(problems, problem => problem.Contains($"Menu item id '{id}'"));
        Assert.Contains(problems, problem => problem.Contains("Route '#/posts'"));
    }

    [Fact]
    public void Create_RejectsIncompleteMenuItemsAndAdministrationPages()
    {
        var module = Module() with
        {
            MenuItems = [new ModuleMenuItem(Guid.Empty, " ", " ", 1)],
            AdministrationPages = [new ModuleAdministrationPage(" ", "fa-x", "/posts", " ")]
        };

        var problems = Problems(module);

        Assert.Contains(problems, problem => problem.Contains("needs a title"));
        Assert.Contains(problems, problem => problem.Contains("needs a url"));
        Assert.Contains(problems, problem => problem.Contains("needs an id"));
        Assert.Contains(problems, problem => problem.Contains("needs a name"));
        Assert.Contains(problems, problem => problem.Contains("must link to a route starting with #/"));
        Assert.Contains(problems, problem => problem.Contains("needs a grant"));
    }

    [Fact]
    public void Create_RejectsRoutesAndStylesOutsideTheModuleFrontendNamespace()
    {
        var module = Module() with
        {
            Routes = [new ModuleRoute("posts", "controllers/posts")],
            Frontend = new ModuleFrontend(typeof(ModuleCatalogTests).Assembly, ["css/posts.css"])
        };

        var problems = Problems(module);

        Assert.Contains(problems, problem => problem.Contains("must start with #/"));
        Assert.Contains(problems, problem => problem.Contains("must use a controller under modules/blog/"));
        Assert.Contains(problems, problem => problem.Contains("Style 'css/posts.css'"));
    }

    [Fact]
    public void Catalog_AggregatesTheContributionsOfEveryModule()
    {
        var other = Module("other") with
        {
            GrantAreas = [new ModuleGrantArea("Other", ModuleAudience.Developer, [GrantActions.Read])],
            Database = new ModuleDatabase(typeof(ModuleCatalogTests).Assembly) { ReadOnlyEntities = ["Audit"] }
        };
        var catalog = ModuleCatalog.Create([NotesModule.Descriptor, other]).GetOrElse(ModuleCatalog.Empty);

        Assert.Equal([NotesModule.Area, "Other"], catalog.GrantAreas.Select(area => area.Area));
        Assert.Equal(2, catalog.MenuItems.Count);
        Assert.Single(catalog.AdministrationPages);
        Assert.Equal([typeof(Note)], catalog.SoftDeletedEntities);
        Assert.Equal([typeof(ModuleCatalogTests).Assembly], catalog.ConfigurationAssemblies);
        Assert.Contains("NoteLog", catalog.ReadOnlyEntities);
        Assert.Contains("Audit", catalog.ReadOnlyEntities);
        Assert.Empty(catalog.Frontends);
    }

    [Theory]
    [InlineData("Settings.json")]
    [InlineData("settings")]
    [InlineData("1settings.json")]
    [InlineData("a/b.json")]
    public void Create_RejectsInvalidConfigurationFiles(string file)
    {
        var problems = Problems(Module() with { ConfigurationFiles = [file] });

        Assert.Contains(problems, problem => problem.Contains($"Configuration file '{file}'"));
    }

    [Fact]
    public void Create_RejectsDuplicateConfigurationFiles()
    {
        var problems = Problems(
            Module("one") with { ConfigurationFiles = ["shared.json"] },
            Module("two") with { ConfigurationFiles = ["shared.json"] });

        Assert.Contains(problems, problem => problem.Contains("Configuration file 'shared.json' is declared more than once"));
    }

    [Fact]
    public void Catalog_AggregatesTheConfigurationFilesOfEveryModule()
    {
        var catalog = ModuleCatalog.Create(
        [
            Module("one") with { ConfigurationFiles = ["one.json"] },
            Module("two") with { ConfigurationFiles = ["two.json", "three.json"] }
        ]).GetOrElse(ModuleCatalog.Empty);

        Assert.Equal(["one.json", "two.json", "three.json"], catalog.ConfigurationFiles);
    }

    [Fact]
    public void FrontendPath_IsNamespacedByTheModuleKey()
    {
        Assert.Equal("modules/notes/", NotesModule.Descriptor.FrontendPath);
    }
}
