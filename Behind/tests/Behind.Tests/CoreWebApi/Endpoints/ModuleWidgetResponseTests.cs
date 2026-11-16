using Behind.Core.WebApi.Endpoints;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ModuleWidgetResponseTests
{
    [Fact]
    public void ModuleResponse_IncludesWidgetDescriptors()
    {
        var module = new ModuleDescriptor("blog", "Blog")
        {
            Widgets = [new ModuleWidget("blog.posts", "widgets.posts", "modules/blog/models/widgets")]
        };

        var response = module.ToResponse();

        Assert.Equal([new ModuleWidgetResponse("blog.posts", "widgets.posts", "modules/blog/models/widgets")],
            response.Widgets);
        Assert.Empty(new ModuleDescriptor("empty", "Empty").ToResponse().Widgets);
    }
}
