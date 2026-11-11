namespace Behind.Tests.Common.Modules;

public class ModuleWidgetTests
{
    [Fact]
    public void Catalog_AggregatesValidWidgets()
    {
        var widget = new ModuleWidget("blog.posts", "widgets.posts", "modules/blog/models/widgets");
        var catalog = ModuleCatalog.Create([new ModuleDescriptor("blog", "Blog") { Widgets = [widget] }])
            .GetOrElse(ModuleCatalog.Empty);

        Assert.Equal([widget], catalog.Widgets);
    }

    [Fact]
    public void Catalog_RejectsInvalidAndDuplicateWidgets()
    {
        var widget = new ModuleWidget("other.posts", "", "modules/other/models/widgets");
        var module = new ModuleDescriptor("blog", "Blog") { Widgets = [widget, widget] };
        var errors = ModuleCatalog.Create([module]).Match(
            problems => problems.Select(problem => problem.ToString()).ToArray(),
            _ => []);

        Assert.Contains(errors, error => error.Contains("Widget key 'other.posts'"));
        Assert.Contains(errors, error => error.Contains("needs a name"));
        Assert.Contains(errors, error => error.Contains("must use a model under modules/blog/"));
        Assert.Contains(errors, error => error.Contains("declared more than once"));
    }
}
