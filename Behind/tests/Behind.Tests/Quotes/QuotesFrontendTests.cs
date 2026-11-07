using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Behind.Quotes.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.Quotes;

public class QuotesFrontendTests
{
    private static readonly ModuleCatalog Catalog =
        ModuleCatalog.Create([QuotesModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider(null, Catalog);

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static JsonElement LocaleFile(string code) =>
        JsonDocument.Parse(Read($"modules/quotes/locales/{code}.json")).RootElement;

    private static IReadOnlyDictionary<string, string> Locale(string code) =>
        LocaleFile(code).GetProperty("messages").Deserialize<Dictionary<string, string>>()!;

    [Theory]
    [InlineData("modules/quotes/controllers/quotes.js")]
    [InlineData("modules/quotes/controllers/quotemanager.js")]
    [InlineData("modules/quotes/viewmodels/quotes.js")]
    [InlineData("modules/quotes/viewmodels/quotemanager.js")]
    [InlineData("modules/quotes/views/quotes.html")]
    [InlineData("modules/quotes/views/quotemanager.html")]
    [InlineData("modules/quotes/models/quotes.js")]
    [InlineData("modules/quotes/locales/en-US.json")]
    [InlineData("modules/quotes/locales/hu-HU.json")]
    public void Provider_ContainsTheFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Provider_WithoutTheModuleDoesNotServeTheFiles()
    {
        var provider = EmbeddedFrontend.CreateFileProvider(null, ModuleCatalog.Empty);

        Assert.False(provider.GetFileInfo("modules/quotes/controllers/quotes.js").Exists);
    }

    [Fact]
    public void Descriptor_RoutesPointAtTheControllersOfTheModule()
    {
        Assert.All(QuotesModule.Descriptor.Routes, route =>
            Assert.True(Provider.GetFileInfo($"{route.Controller}.js").Exists, route.Controller));
    }

    [Theory]
    [InlineData("quotes", "QuotesViewModel")]
    [InlineData("quotemanager", "QuoteManagerViewModel")]
    public void Controllers_BindTheirViewAndViewModel(string name, string viewModel)
    {
        var controller = Read($"modules/quotes/controllers/{name}.js");

        Assert.Contains($"text!modules/quotes/views/{name}.html", controller);
        Assert.Contains($"modules/quotes/viewmodels/{name}", controller);
        Assert.Contains($"createController(view, {viewModel})", controller);
    }

    [Fact]
    public void Model_CallsTheQuoteEndpoints()
    {
        var model = Read("modules/quotes/models/quotes.js");

        Assert.Contains("\"/quotes?\"", model);
        Assert.Contains("\"/quotes/manage\"", model);
        Assert.Contains("http.post(\"/quotes\"", model);
        Assert.Contains("\"/delete\"", model);
        Assert.Contains("\"/restore\"", model);
        Assert.Contains("\"/hard/delete\"", model);
        Assert.Contains("\"version-conflict\"", model);
    }

    [Fact]
    public void ManagerViewModel_UsesTheGrantsOfTheModule()
    {
        var viewModel = Read("modules/quotes/viewmodels/quotemanager.js");

        Assert.Contains("hasGrant(\"Quotes.Read\")", viewModel);
        Assert.Contains("hasGrant(\"Quotes.Write\")", viewModel);
        Assert.Contains("hasGrant(\"Quotes.Delete\")", viewModel);
        Assert.Contains("row.restore", viewModel);
        Assert.Contains("row.hardDelete", viewModel);
    }

    [Fact]
    public void ManagerView_OffersSoftDeleteRestoreAndPermanentDelete()
    {
        var view = Read("modules/quotes/views/quotemanager.html");

        Assert.Contains("click: remove", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("showDeleted", view);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_AreBundlesWithANameAndMessagesLikeTheCoreOnes(string code)
    {
        var file = LocaleFile(code);

        Assert.False(string.IsNullOrWhiteSpace(file.GetProperty("name").GetString()));
        Assert.Equal(JsonValueKind.Object, file.GetProperty("messages").ValueKind);
    }

    [Fact]
    public void Locales_HaveTheSameKeysInEveryLanguage()
    {
        var english = Locale("en-US");
        var hungarian = Locale("hu-HU");

        Assert.Equal(english.Keys.Order(), hungarian.Keys.Order());
        Assert.All(english.Values.Concat(hungarian.Values), text => Assert.False(string.IsNullOrWhiteSpace(text)));
        Assert.Contains(QuotesModuleNamesKey, english.Keys);
    }

    private const string QuotesModuleNamesKey = Behind.Quotes.Common.QuotesModuleNames.AdministrationNameKey;

    [Theory]
    [InlineData("en-US", "Quotes")]
    [InlineData("hu-HU", "Idézetek")]
    public void Locales_TranslateTheMenuItemOfTheModule(string code, string title)
    {
        Assert.Equal(title, Locale(code)["menu.item.quotes"]);
        Assert.Equal("Quotes", Assert.Single(QuotesModule.Descriptor.MenuItems).Title);
    }

    [Fact]
    public void Locales_ContainEveryKeyTheFrontendUses()
    {
        var english = Locale("en-US");
        var sources = new[]
        {
            "modules/quotes/models/quotes.js",
            "modules/quotes/viewmodels/quotes.js",
            "modules/quotes/viewmodels/quotemanager.js",
            "modules/quotes/views/quotes.html",
            "modules/quotes/views/quotemanager.html"
        }.Select(Read);

        var used = sources
            .SelectMany(source => System.Text.RegularExpressions.Regex.Matches(source, "[\"']((?:quotes)\\.[A-Za-z.]+)[\"']"))
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.True(english.ContainsKey(key), key));
    }
}
