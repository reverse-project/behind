using System.Net;
using System.Text;
using System.Text.Json;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class LocalizationFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("models/i18n.js")]
    [InlineData("models/usersettings.js")]
    [InlineData("locales/en-US.json")]
    [InlineData("locales/hu-HU.json")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Theory]
    [InlineData("en-US", "English (International)")]
    [InlineData("hu-HU", "Magyar")]
    public void Locale_HasItsOfficialName(string locale, string name)
    {
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));

        Assert.Equal(name, document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void Engine_LoadsTheFallbackAndTheRequestedLocaleFromTheLocalesFolder()
    {
        var engine = Read("models/i18n.js");

        Assert.Contains("var fallbackLocale = \"en-US\"", engine);
        Assert.Contains("http.get(\"/locales/\" + encodeURIComponent(code) + \".json\")", engine);
        Assert.Contains("Object.assign({}, loaded[0], loaded[1])", engine);
        Assert.Contains("ko.bindingHandlers.t =", engine);
        Assert.Contains("ko.bindingHandlers.tAttr =", engine);
        Assert.Contains("document.documentElement.setAttribute(\"lang\", locale())", engine);
        Assert.DoesNotContain("<!--", engine);
    }

    [Fact]
    public void App_LoadsTheDefaultLocaleOfTheSiteBeforeBindingTheShell()
    {
        var app = Read("app.js");

        Assert.Contains("i18n.useDefault(branding.locale())", app);
        Assert.True(app.IndexOf("i18n.apply()", StringComparison.Ordinal) < app.IndexOf("router.run", StringComparison.Ordinal));
        Assert.Contains("defaultLocale: data.defaultLocale", Read("models/about.js"));
        Assert.Contains("locale(site.defaultLocale || \"\")", Read("models/branding.js"));
    }

    [Fact]
    public void App_FollowsTheLocaleOfTheSignedInUserAndLoadsTheSettingsAgainWhenTheSessionChanges()
    {
        var app = Read("app.js");

        Assert.Contains("session.isSignedIn() ? usersettings.load() : usersettings.clear()", app);
        Assert.Contains("usersettings.locale.subscribe(i18n.useAccount)", app);
        Assert.Contains("session.isSignedIn.subscribe(syncUserSettings)", app);
        Assert.Contains("session.isImpersonating.subscribe(syncUserSettings)", app);
    }

    [Fact]
    public void Engine_PrefersTheAccountThenTheBrowserThenTheSiteDefault()
    {
        var engine = Read("models/i18n.js");

        Assert.Contains("account() || remembered() || siteDefault || fallbackLocale", engine);
        Assert.Contains("window.localStorage.setItem(browserKey, code)", engine);
        Assert.Contains("if (current !== ticket)", engine);
    }

    [Fact]
    public void UserSettingsModel_CallsTheProfileSettingsEndpoints()
    {
        var model = Read("models/usersettings.js");

        Assert.Contains("http.get(\"/profile/settings\")", model);
        Assert.Contains("http.put(\"/profile/settings/\" + encodeURIComponent(key), { value: value })", model);
        Assert.Contains("var localeKey = \"locale\"", model);
    }

    [Fact]
    public void ProfilePage_SavesTheLanguageToTheAccountWhenSignedInAndToTheBrowserOtherwise()
    {
        var viewModel = Read("viewmodels/profile.js");
        var view = Read("views/profile.html");

        Assert.Contains("usersettings.saveLocale(code)", viewModel);
        Assert.Contains("i18n.choose(code)", viewModel);
        Assert.Contains("about.loadLocales()", viewModel);
        Assert.Contains("submit: submitLocale", view);
        Assert.Contains("options: locales, optionsValue: 'code', optionsText: 'name', value: locale", view);
    }

    [Fact]
    public void SettingsView_ChoosesFieldsWithOptionsFromASelect()
    {
        var view = Read("views/settings.html");
        var viewModel = Read("viewmodels/settings.js");

        Assert.Contains("options: options, optionsValue: 'value', optionsText: 'label'", view);
        Assert.Contains("editable && isText && !sensitive && !hasOptions", view);
        Assert.Contains("hasOptions: !!field.options", viewModel);
    }

    [Fact]
    public async Task WebRoot_OverridesAnEmbeddedLocaleAndAddsNewOnes()
    {
        using var webRoot = new TempFrontendDirectory(
            new Dictionary<string, string>
            {
                ["locales/hu-HU.json"] = """{ "name": "Magyar", "messages": { "app.signIn": "Belépés" } }""",
                ["locales/de-DE.json"] = """{ "name": "Deutsch", "messages": { "app.signIn": "Anmelden" } }"""
            });
        await using var host = await StaticFrontendHost.StartAsync(
            Provider.WithWebRoot(webRoot.Root),
            new FrontendOptions { Minify = false, Compress = false });

        var hungarian = await host.Client.GetStringAsync("/locales/hu-HU.json");
        var german = await host.Client.GetStringAsync("/locales/de-DE.json");
        var english = await host.Client.GetAsync("/locales/en-US.json");

        Assert.Contains("Belépés", hungarian);
        Assert.Contains("Anmelden", german);
        Assert.Equal(HttpStatusCode.OK, english.StatusCode);
    }
}
