using System.Text.Json;
using System.Text.RegularExpressions;
using Behind.Core.WebApi;
using Behind.Forum.Common;
using Behind.Forum.Domain;
using Behind.Forum.WebApi;
using Behind.Forum.WebApi.Endpoints;

namespace Behind.Tests.Forum;

public sealed class ForumFrontendTests
{
    private static readonly ModuleCatalog Catalog =
        ModuleCatalog.Create([ForumModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

    private static string Read(string path)
    {
        var file = EmbeddedFrontend.CreateFileProvider(null, Catalog).GetFileInfo(path);
        Assert.True(file.Exists, path);
        using var reader = new StreamReader(file.CreateReadStream());
        return reader.ReadToEnd();
    }

    private static Dictionary<string, string> Locale(string code) =>
        JsonDocument.Parse(Read($"modules/forum/locales/{code}.json")).RootElement
            .GetProperty("messages").Deserialize<Dictionary<string, string>>()!;

    [Fact]
    public void Frontend_AllRoutesHaveControllersViewsAndViewModels()
    {
        foreach (var route in ForumModule.Descriptor.Routes)
        {
            var controller = Read(route.Controller + ".js");
            var name = route.Controller.Split('/')[^1];
            Assert.Contains($"text!modules/forum/views/{name}.html", controller);
            Assert.Contains($"modules/forum/viewmodels/{name}", controller);
            Assert.NotEmpty(Read($"modules/forum/views/{name}.html"));
            Assert.NotEmpty(Read($"modules/forum/viewmodels/{name}.js"));
        }
    }

    [Fact]
    public void Locales_ContainEveryModuleTranslationAndUseTheSameKeys()
    {
        var english = Locale("en-US");
        var hungarian = Locale("hu-HU");
        Assert.Equal(english.Keys.Order(), hungarian.Keys.Order());
        Assert.All(english.Values.Concat(hungarian.Values), value => Assert.False(string.IsNullOrWhiteSpace(value)));

        var files = new[]
        {
            "views/forum.html", "views/forummanager.html", "viewmodels/forum.js",
            "viewmodels/forummanager.js", "bindings/bbcode.js"
        };
        foreach (var file in files)
        {
            var text = Read($"modules/forum/{file}");
            foreach (Match match in Regex.Matches(text, "['\"](forum\\.[A-Za-z]+)['\"]"))
            {
                var key = match.Groups[1].Value;
                Assert.Contains(key, english.Keys);
            }
        }
    }

    [Fact]
    public void Json_UsesStringEnumsSoBrowserFormsAndSettingsRoundTrip()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Equal("\"Pending\"", JsonSerializer.Serialize(PublicationState.Pending, options));
        Assert.Equal("\"Pre\"", JsonSerializer.Serialize(ModerationMode.Pre, options));
        Assert.Equal(PublicationState.Published,
            JsonSerializer.Deserialize<ForumEndpoints.ReviewRequest>("{\"state\":\"Published\"}", options)!.State);
        Assert.Equal(ModerationMode.Pre,
            JsonSerializer.Deserialize<ForumEndpoints.UserRuleRequest>(
                "{\"userId\":\"11111111-1111-1111-1111-111111111111\",\"moderation\":\"Pre\"}", options)!.Moderation);
    }

    [Fact]
    public void Messages_UseSafeBBCodeAndAllManagedResourcesHaveDeletionControls()
    {
        var publicView = Read("modules/forum/views/forum.html");
        Assert.Contains("bbcode: body", publicView);
        Assert.Contains("bbcodeEditor:", publicView);
        Assert.Contains("text: author || $root.unknownAuthor", publicView);
        Assert.Contains("forum-topic-author", publicView);
        Assert.Contains("forum-post-author", publicView);
        Assert.DoesNotContain("html:", publicView);
        var manager = Read("modules/forum/views/forummanager.html");
        Assert.Contains("bbcode: body", manager);
        Assert.Contains("modules/forum/bindings/bbcode", Read("modules/forum/controllers/forum.js"));
        Assert.Contains("modules/forum/bindings/bbcode", Read("modules/forum/controllers/forummanager.js"));
        Assert.NotEmpty(Read("modules/forum/models/bbcode.js"));
        Assert.NotEmpty(Read("modules/forum/bindings/bbcode.js"));
        Assert.NotEqual("User", Locale("en-US")["forum.unknownAuthor"]);
        Assert.Contains("foreach: boardGroups", publicView);
        Assert.Contains("forum-category-heading", publicView);
        Assert.Contains("forum-board-category", manager);
        Assert.Equal(4, Regex.Matches(manager, "click: remove").Count);
        Assert.Equal(4, Regex.Matches(manager, "click: restore").Count);
        Assert.Equal(4, Regex.Matches(manager, "click: hardDelete").Count);
        Assert.NotEmpty(Read("models/profilepicture.js"));
        Assert.Contains("choosePicture", Read("views/profile.html"));
    }
}
