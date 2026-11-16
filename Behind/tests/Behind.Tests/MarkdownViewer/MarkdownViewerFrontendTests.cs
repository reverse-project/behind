using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Behind.Core.WebApi;
using Behind.MarkdownViewer.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.MarkdownViewer;

public partial class MarkdownViewerFrontendTests
{
    private static readonly ModuleCatalog Catalog =
        ModuleCatalog.Create([MarkdownViewerModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider(null, Catalog);

    [GeneratedRegex("t: '([^']+)'")]
    private static partial Regex ViewKeyPattern { get; }

    [GeneratedRegex(@"i18n\.t(?:Or)?\(""([^""]*[A-Za-z0-9])""")]
    private static partial Regex ScriptKeyPattern { get; }

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static IReadOnlyDictionary<string, string> Locale(string code) =>
        JsonDocument.Parse(Read($"modules/markdownviewer/locales/{code}.json")).RootElement
            .GetProperty("messages").Deserialize<Dictionary<string, string>>()!;

    [Theory]
    [InlineData("modules/markdownviewer/controllers/docs.js")]
    [InlineData("modules/markdownviewer/controllers/markdownmanager.js")]
    [InlineData("modules/markdownviewer/viewmodels/docs.js")]
    [InlineData("modules/markdownviewer/viewmodels/markdownmanager.js")]
    [InlineData("modules/markdownviewer/views/docs.html")]
    [InlineData("modules/markdownviewer/views/markdownmanager.html")]
    [InlineData("modules/markdownviewer/models/markdownviewer.js")]
    [InlineData("modules/markdownviewer/css/markdownviewer.css")]
    [InlineData("modules/markdownviewer/locales/en-US.json")]
    [InlineData("modules/markdownviewer/locales/hu-HU.json")]
    public void Provider_ContainsTheFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Provider_WithoutTheModuleDoesNotServeTheFiles()
    {
        var provider = EmbeddedFrontend.CreateFileProvider(null, ModuleCatalog.Empty);

        Assert.False(provider.GetFileInfo("modules/markdownviewer/controllers/docs.js").Exists);
    }

    [Fact]
    public void Descriptor_RoutesPointAtTheControllersOfTheModule()
    {
        Assert.All(MarkdownViewerModule.Descriptor.Routes, route =>
            Assert.True(Provider.GetFileInfo($"{route.Controller}.js").Exists, route.Controller));
    }

    [Fact]
    public void Descriptor_StylesAreServed()
    {
        Assert.All(MarkdownViewerModule.Descriptor.Frontend.AsEnumerable().SelectMany(frontend => frontend.Styles), style =>
            Assert.True(Provider.GetFileInfo(style).Exists, style));
    }

    [Fact]
    public void Locales_HaveTheSameKeys()
    {
        Assert.Equal(Locale("en-US").Keys.Order(), Locale("hu-HU").Keys.Order());
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_HaveNoEmptyMessages(string code)
    {
        Assert.All(Locale(code), message => Assert.False(string.IsNullOrWhiteSpace(message.Value), message.Key));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_CoverTheKeysUsedByTheViewsAndScripts(string code)
    {
        var locale = Locale(code);
        var used = new[]
            {
                "modules/markdownviewer/views/docs.html",
                "modules/markdownviewer/views/markdownmanager.html"
            }
            .SelectMany(path => ViewKeyPattern.Matches(Read(path)).Select(match => match.Groups[1].Value))
            .Concat(ScriptKeyPattern.Matches(Read("modules/markdownviewer/models/markdownviewer.js")).Select(match => match.Groups[1].Value))
            .Where(key => key.StartsWith("markdownviewer.", StringComparison.Ordinal))
            .Append(MarkdownViewerModule.Descriptor.AdministrationPages.Single().Name)
            .Append("menu.item.documentation")
            .Distinct();

        Assert.All(used, key => Assert.True(locale.ContainsKey(key), key));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_CoverTheGenerationProblems(string code)
    {
        var locale = Locale(code);

        Assert.All(
            new[]
            {
                Behind.MarkdownViewer.Common.MarkdownWorkspaceErrors.SourceNotConfigured,
                Behind.MarkdownViewer.Common.MarkdownWorkspaceErrors.SourceMissing,
                Behind.MarkdownViewer.Common.MarkdownWorkspaceErrors.OutputNotConfigured,
                Behind.MarkdownViewer.Common.MarkdownWorkspaceErrors.OutputOverlapsSource,
                Behind.MarkdownViewer.Common.MarkdownWorkspaceErrors.GenerationFailed
            },
            error => Assert.True(locale.ContainsKey($"markdownviewer.error.{error}"), error));
    }

    [Fact]
    public void Model_CallsTheMarkdownViewerEndpoints()
    {
        var model = Read("modules/markdownviewer/models/markdownviewer.js");

        Assert.Contains("localized(\"/markdownviewer/documents\", locale)", model);
        Assert.Contains("\"/markdownviewer/documents/\"", model);
        Assert.Contains("\"?locale=\" + encodeURIComponent(locale)", model);
        Assert.Contains("http.post(\"/markdownviewer/generate\")", model);
    }

    [Fact]
    public void ManagerViewModel_UsesTheModuleGrants()
    {
        var viewModel = Read("modules/markdownviewer/viewmodels/markdownmanager.js");

        Assert.Contains("session.hasGrant(\"MarkdownViewer.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"MarkdownViewer.Write\")", viewModel);
    }

    [Fact]
    public void Viewer_KeepsInPageAnchorsOutOfTheRouter()
    {
        var viewModel = Read("modules/markdownviewer/viewmodels/docs.js");
        var view = Read("modules/markdownviewer/views/docs.html");

        Assert.Contains("scrollIntoView", viewModel);
        Assert.Contains("html: html, click: follow", view);
    }

    [Fact]
    public void Viewer_FollowsTheLanguageSelectorOfTheApplication()
    {
        var viewModel = Read("modules/markdownviewer/viewmodels/docs.js");
        var view = Read("modules/markdownviewer/views/docs.html");

        Assert.Contains("markdownviewer.list(wantedLocale())", viewModel);
        Assert.Contains("markdownviewer.page(path, wantedLocale())", viewModel);
        Assert.Contains("context.params.locale", viewModel);
        Assert.Contains("i18n.locale.subscribe(load)", viewModel);
        Assert.Contains("text: fallbackNotice", view);
    }
}
