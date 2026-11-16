using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Behind.Core.WebApi;
using Behind.Sport.Common;
using Behind.Sport.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.Sport;

public class SportFrontendTests
{
    private static readonly ModuleCatalog Catalog =
        ModuleCatalog.Create([SportModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider(null, Catalog);

    private static readonly string[] Pages =
    [
        "sport", "discipline", "teams", "team", "players", "player", "seasons", "season", "leaderboards", "stats", "manager"
    ];

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static JsonElement LocaleFile(string code) =>
        JsonDocument.Parse(Read($"modules/sport/locales/{code}.json")).RootElement;

    private static IReadOnlyDictionary<string, string> Locale(string code) =>
        LocaleFile(code).GetProperty("messages").Deserialize<Dictionary<string, string>>()!;

    public static IEnumerable<object[]> PageNames => Pages.Select(page => new object[] { page });

    [Theory]
    [MemberData(nameof(PageNames))]
    public void Provider_ContainsEveryPartOfThePage(string page)
    {
        Assert.True(Provider.GetFileInfo($"modules/sport/controllers/{page}.js").Exists);
        Assert.True(Provider.GetFileInfo($"modules/sport/viewmodels/{page}.js").Exists);
        Assert.True(Provider.GetFileInfo($"modules/sport/views/{page}.html").Exists);
    }

    [Theory]
    [InlineData("modules/sport/models/sport.js")]
    [InlineData("modules/sport/models/links.js")]
    [InlineData("modules/sport/models/resources.js")]
    [InlineData("modules/sport/locales/en-US.json")]
    [InlineData("modules/sport/locales/hu-HU.json")]
    public void Provider_ContainsTheSharedFiles(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Provider_WithoutTheModuleDoesNotServeTheFiles()
    {
        var provider = EmbeddedFrontend.CreateFileProvider(null, ModuleCatalog.Empty);

        Assert.False(provider.GetFileInfo("modules/sport/controllers/sport.js").Exists);
    }

    [Fact]
    public void Descriptor_RoutesPointAtTheControllersOfTheModule()
    {
        Assert.All(SportModule.Descriptor.Routes, route =>
            Assert.True(Provider.GetFileInfo($"{route.Controller}.js").Exists, route.Controller));
    }

    [Theory]
    [MemberData(nameof(PageNames))]
    public void Controllers_BindTheirViewAndViewModel(string page)
    {
        var controller = Read($"modules/sport/controllers/{page}.js");

        Assert.Contains($"text!modules/sport/views/{page}.html", controller);
        Assert.Contains($"modules/sport/viewmodels/{page}", controller);
        Assert.Contains("createController(view,", controller);
    }

    [Fact]
    public void Model_CallsTheSportEndpoints()
    {
        var model = Read("modules/sport/models/sport.js");

        Assert.Contains("\"/sport/\"", model);
        Assert.Contains("\"/manage\"", model);
        Assert.Contains("\"/delete\"", model);
        Assert.Contains("\"/restore\"", model);
        Assert.Contains("\"/hard/delete\"", model);
        Assert.Contains("\"/images\"", model);
        Assert.Contains("leaderboards/", model);
        Assert.All(
            ["version-conflict", "unknown-reference", "mismatch", "duplicate", "unknown-image", "invalid-range"],
            reason => Assert.Contains($"\"{reason}\"", model));
    }

    [Fact]
    public void Resources_CoverEveryResourceOfTheBackend()
    {
        var resources = Read("modules/sport/models/resources.js");

        Assert.All(
            ["disciplines", "positions", "stat-types", "seasons", "teams", "players", "player-seasons", "stats"],
            resource => Assert.Contains($"\"{resource}\"", resources));
    }

    [Fact]
    public void ManagerViewModel_UsesTheGrantsOfTheModule()
    {
        var viewModel = Read("modules/sport/viewmodels/manager.js");

        Assert.Contains("hasGrant(\"Sport.Read\")", viewModel);
        Assert.Contains("hasGrant(\"Sport.Write\")", viewModel);
        Assert.Contains("hasGrant(\"Sport.Delete\")", viewModel);
        Assert.Contains("row.restore", viewModel);
        Assert.Contains("row.hardDelete", viewModel);
    }

    [Fact]
    public void ManagerView_OffersSoftDeleteRestoreAndPermanentDelete()
    {
        var view = Read("modules/sport/views/manager.html");

        Assert.Contains("click: remove", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("showDeleted", view);
    }

    [Fact]
    public void Links_PointAtThePagesOfTheModuleRoutes()
    {
        var links = Read("modules/sport/models/links.js");
        var paths = SportModule.Descriptor.Routes.Select(route => route.Path).ToList();

        Assert.All(
            ["#/sport", "\"disciplines\"", "\"teams\"", "\"players\"", "\"seasons\"", "leaderboards", "stats", "players/position", "#/manage/sport"],
            fragment => Assert.Contains(fragment, links));
        Assert.Contains("#/sport/teams/:id", paths);
        Assert.Contains("#/sport/players/:id", paths);
        Assert.Contains("#/sport/seasons/:id", paths);
        Assert.Contains("#/sport/disciplines/:id", paths);
        Assert.Contains("#/sport/players/position/:position", paths);
        Assert.Contains("#/sport/leaderboards/:season/:statType", paths);
    }

    [Theory]
    [InlineData("team")]
    [InlineData("player")]
    [InlineData("season")]
    public void DetailViews_LinkTheirEntitiesAndShowTheirPicturesOrLogos(string page)
    {
        var view = Read($"modules/sport/views/{page}.html");

        Assert.Contains("sport-link", view);
    }

    [Fact]
    public void PlayerProfile_ShowsAgeWithoutExactBirthDate()
    {
        var view = Read("modules/sport/views/player.html");

        Assert.Contains("'sport.field.age'", view);
        Assert.DoesNotContain("birthDate", view, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains(SportModuleNames.AdministrationNameKey, english.Keys);
    }

    [Theory]
    [InlineData("en-US", "{0} {1}")]
    [InlineData("hu-HU", "{1} {0}")]
    public void Locales_OrderThePartsOfAPersonName(string code, string template)
    {
        Assert.Equal(template, Locale(code)["sport.personName"]);
    }

    [Theory]
    [InlineData("en-US", "Sport")]
    [InlineData("hu-HU", "Sport")]
    public void Locales_TranslateTheMenuItemOfTheModule(string code, string title)
    {
        Assert.Equal(title, Locale(code)["menu.item.sport"]);
        Assert.Equal("Sport", Assert.Single(SportModule.Descriptor.MenuItems).Title);
    }

    [Fact]
    public void Locales_ContainEveryKeyTheFrontendUses()
    {
        var english = Locale("en-US");
        var files = Pages;
        var texts = new List<string>
        {
            Read("modules/sport/models/sport.js"),
            Read("modules/sport/models/resources.js"),
            Read("modules/sport/models/resourceform.js"),
            Read("modules/sport/models/format.js"),
            Read("modules/sport/models/listing.js"),
            Read("modules/sport/models/seasonbrowser.js"),
            Read("modules/sport/models/detail.js")
        };
        texts.AddRange(files.Select(page => Read($"modules/sport/viewmodels/{page}.js")));
        texts.AddRange(files.Select(page => Read($"modules/sport/views/{page}.html")));
        texts.AddRange(
            new[] { "link", "thumbnail", "leaderboard", "stats", "browser" }.SelectMany(name =>
                new[] { Read($"modules/sport/components/{name}.js"), Read($"modules/sport/components/{name}.html") }));

        var used = texts
            .SelectMany(source => Regex.Matches(source, "[\"']((?:sport)\\.[A-Za-z0-9.]+)[\"']"))
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.True(english.ContainsKey(key), key));
    }

    [Fact]
    public void Locales_DoNotKeepKeysTheFrontendNeverUses()
    {
        var english = Locale("en-US");
        var all = new List<string>();

        foreach (var directory in new[] { "controllers", "viewmodels", "views", "models", "components" })
        {
            foreach (var entry in Provider.GetDirectoryContents($"modules/sport/{directory}"))
            {
                all.Add(Read($"modules/sport/{directory}/{entry.Name}"));
            }
        }

        var unused = english.Keys
            .Where(key => key != "menu.item.sport" && key != SportModuleNames.AdministrationNameKey)
            .Where(key => !key.StartsWith("audit.entities.", StringComparison.Ordinal))
            .Where(key => !all.Any(source => source.Contains($"\"{key}\"") || source.Contains($"'{key}'")))
            .ToList();

        Assert.Empty(unused);
    }
}
