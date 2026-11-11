using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Behind.Core.WebApi;

namespace Behind.Tests.CoreWebApi;

public sealed class LayoutsFrontendTests
{
    private static string Read(string path)
    {
        var file = EmbeddedFrontend.CreateFileProvider().GetFileInfo(path);
        Assert.True(file.Exists, path);
        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("controllers/layouts.js")]
    [InlineData("viewmodels/layouts.js")]
    [InlineData("views/layouts.html")]
    [InlineData("models/layouts.js")]
    [InlineData("models/layoutshell.js")]
    public void EmbeddedFrontend_ContainsLayoutSurface(string path) => Assert.NotEmpty(Read(path));

    [Fact]
    public void App_WiresEditorShellAndWidgetComponent()
    {
        var app = Read("app.js");
        Assert.Contains("\"#/manage/layouts\": \"controllers/layouts\"", app);
        Assert.Contains("ko.components.register(\"site-widget\"", app);
        Assert.Contains("layoutShell.start()", app);
        Assert.Contains("layoutShell.route(", app);
        Assert.Contains("#/manage/layouts", Read("viewmodels/administration.js"));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_TranslateAllLayoutTexts(string locale)
    {
        var sources = Read("views/layouts.html") + Read("viewmodels/layouts.js") + Read("models/layoutshell.js");
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
        var messages = document.RootElement.GetProperty("messages");
        var keys = Regex.Matches(sources, "[\"'](layouts\\.[A-Za-z0-9.]+)[\"']")
            .Select(match => match.Groups[1].Value).Distinct();
        foreach (var key in keys)
        {
            Assert.True(messages.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()), key);
        }
    }

    [Fact]
    public void Grid_UsesResponsiveStackingAndUnconstrainedContent()
    {
        var css = Read("css/app.css");
        Assert.Contains(".behind-layout-section", css);
        Assert.Contains(".behind-layout .behind-layout-content", css);
        Assert.Contains(".behind-layout > .behind-layout-section", css);
        Assert.Contains("flex-direction: column", css);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_TranslateAllWidgetTexts(string locale)
    {
        var sources = Read("models/widgets.js") + Read("components/sitewidget.js") + Read("components/sitewidget.html");
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
        var messages = document.RootElement.GetProperty("messages");
        var keys = Regex.Matches(sources, "[\"'](widgets\\.[A-Za-z0-9.]+)[\"']")
            .Select(match => match.Groups[1].Value).Distinct();
        foreach (var key in keys)
        {
            Assert.True(messages.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()), key);
        }
    }
}
