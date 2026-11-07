using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class ModuleFrontendTests
{
    private static readonly ModuleCatalog WithFrontend = ModuleCatalog.Create(
        [NotesModule.Descriptor with { Frontend = new ModuleFrontend(typeof(NotesModule).Assembly) }])
        .GetOrElse(ModuleCatalog.Empty);

    private static string Read(IFileProvider provider, string path)
    {
        var file = provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ReadCore(string path) => Read(EmbeddedFrontend.CreateFileProvider(), path);

    [Fact]
    public void CreateFileProvider_ServesTheFrontendFilesOfTheModules()
    {
        var provider = EmbeddedFrontend.CreateFileProvider(null, WithFrontend);

        Assert.Contains("notes.administration", Read(provider, "modules/notes/locales/en-US.json"));
        Assert.Contains("NotesController", Read(provider, "modules/notes/controllers/notes.js"));
    }

    [Fact]
    public void CreateFileProvider_KeepsServingTheCoreFrontend()
    {
        var provider = EmbeddedFrontend.CreateFileProvider(null, WithFrontend);

        Assert.Equal(ReadCore("app.js"), Read(provider, "app.js"));
        Assert.Equal(ReadCore("locales/en-US.json"), Read(provider, "locales/en-US.json"));
    }

    [Fact]
    public void CreateFileProvider_WithoutModulesDoesNotServeModuleFiles()
    {
        var provider = EmbeddedFrontend.CreateFileProvider(null, ModuleCatalog.Empty);

        Assert.False(provider.GetFileInfo("modules/notes/locales/en-US.json").Exists);
    }

    [Fact]
    public async Task StaticFrontend_ServesTheModuleFilesNextToTheCoreOnes()
    {
        await using var host = await StaticFrontendHost.StartAsync(EmbeddedFrontend.CreateFileProvider(null, WithFrontend));

        var module = await host.Client.GetAsync("/modules/notes/locales/en-US.json");
        var core = await host.Client.GetAsync("/app.js");

        Assert.True(module.IsSuccessStatusCode);
        Assert.Contains("notes.administration", await module.Content.ReadAsStringAsync());
        Assert.True(core.IsSuccessStatusCode);
    }

    [Fact]
    public void Frontend_LoadsTheModulesBeforeTheRoutesAreRegistered()
    {
        var app = ReadCore("app.js");

        Assert.Contains("\"models/modules\"", app);
        Assert.Contains("Promise.all([branding.load(), modules.load()])", app);
        Assert.Contains("router = createRouter();", app);
        Assert.True(
            app.IndexOf("[routes, moduleRouteTable(), siteRoutes]", StringComparison.Ordinal)
            < app.IndexOf("app.get(aliasRoute", StringComparison.Ordinal));
        Assert.Contains("route.open", app);
    }

    [Fact]
    public void ModulesModel_ReadsTheModulesEndpointAndLoadsTheStyles()
    {
        var model = ReadCore("models/modules.js");

        Assert.Contains("http.get(\"/modules\")", model);
        Assert.Contains("routes: function", model);
        Assert.Contains("administrationPages: function", model);
        Assert.Contains("localeUrls: function", model);
        Assert.Contains("link.rel = \"stylesheet\"", model);
    }

    [Fact]
    public void Administration_ListsTheAdministrationPagesOfTheModulesBehindTheirGrants()
    {
        var administration = ReadCore("viewmodels/administration.js");

        Assert.Contains("\"models/modules\"", administration);
        Assert.Contains("modules.administrationPages()", administration);
        Assert.Contains("session.hasGrant(page.grant)", administration);
        Assert.Contains("corePages.concat(modulePages())", administration);
    }

    [Fact]
    public void Localization_MergesTheMessagesOfTheModulesUnderTheCoreMessages()
    {
        var i18n = ReadCore("models/i18n.js");

        Assert.Contains("modules.localeUrls(code)", i18n);
        Assert.Contains("Object.assign({}, loaded[1], loaded[0])", i18n);
    }
}
