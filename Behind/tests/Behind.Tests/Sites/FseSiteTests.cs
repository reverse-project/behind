using System.Text.Json;
using System.Xml.Linq;

namespace Behind.Tests.Sites;

public class FseSiteTests
{
    private static DirectoryInfo SolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory;
    }

    private static string SiteFile(string name) =>
        Path.Combine(SolutionDirectory().FullName, "src", "Sites", "Behind.Sites.FSE", name);

    private static string Read(string name)
    {
        var path = SiteFile(name);
        Assert.True(File.Exists(path), name);

        return File.ReadAllText(path);
    }

    private static JsonElement ReadJson(string name)
    {
        using var document = JsonDocument.Parse(Read(name));
        return document.RootElement.Clone();
    }

    [Theory]
    [InlineData("wwwroot/index.html")]
    [InlineData("wwwroot/css/fse.css")]
    [InlineData("wwwroot/favicon.svg")]
    [InlineData("wwwroot/favicon.ico")]
    [InlineData("wwwroot/favicon-96x96.png")]
    [InlineData("wwwroot/apple-touch-icon.png")]
    [InlineData("wwwroot/web-app-manifest-192x192.png")]
    [InlineData("wwwroot/web-app-manifest-512x512.png")]
    [InlineData("wwwroot/site.webmanifest")]
    public void Wwwroot_ContainsOverrideFile(string path)
    {
        Assert.True(File.Exists(SiteFile(path)), path);
    }

    [Fact]
    public void IndexHtml_LoadsSkinAfterAppCss()
    {
        var index = Read("wwwroot/index.html");
        var appCss = index.IndexOf("css/app.css", StringComparison.Ordinal);
        var skin = index.IndexOf("css/fse.css", StringComparison.Ordinal);

        Assert.True(appCss >= 0, "css/app.css");
        Assert.True(skin > appCss, "css/fse.css after css/app.css");
    }

    [Theory]
    [InlineData("<header id=\"header-container\">")]
    [InlineData("<main id=\"main-container\"")]
    [InlineData("<footer id=\"footer-container\"")]
    [InlineData("libraries/require.min.js")]
    [InlineData("data-main=\"bootloader.js\"")]
    [InlineData("spinner-border")]
    public void IndexHtml_KeepsTheEmbeddedShell(string marker)
    {
        Assert.Contains(marker, Read("wwwroot/index.html"));
    }

    [Theory]
    [InlineData("fse-brand")]
    [InlineData("fse-brand-logo")]
    [InlineData("fse-brand-name")]
    [InlineData("fse-menu")]
    [InlineData("fse-contact")]
    [InlineData("fse-bottom")]
    [InlineData("/favicon.svg")]
    [InlineData("4621 Fényeslitke, Rákóczi u. 1.")]
    [InlineData("https://www.facebook.com/fenyeslitkeisportegyesulet")]
    [InlineData("mailto:info@fenyeslitkese.hu")]
    public void IndexHtml_LooksLikeTheActualSite(string marker)
    {
        Assert.Contains(marker, Read("wwwroot/index.html"));
    }

    [Fact]
    public void IndexHtml_UsesFseBranding()
    {
        var index = Read("wwwroot/index.html");

        Assert.Contains("<title>Fényeslitke Sportegyesület</title>", index);
        Assert.Contains("content=\"FSE\"", index);
    }

    [Fact]
    public void IndexHtml_ForcesTheLightColorScheme()
    {
        Assert.Contains("<meta name=\"color-scheme\" content=\"light\">", Read("wwwroot/index.html"));
    }

    [Theory]
    [InlineData("#195d10")]
    [InlineData("#ffe325")]
    [InlineData("#333333")]
    [InlineData("--bs-primary")]
    [InlineData("#header-container")]
    [InlineData("#footer-container")]
    [InlineData(".fse-brand")]
    [InlineData(".fse-menu")]
    [InlineData(".fse-contact")]
    [InlineData(".behind-carousel")]
    [InlineData(".ql-snow .ql-tooltip .ql-link-target input")]
    public void Skin_UsesTheFseDesign(string marker)
    {
        Assert.Contains(marker, Read("wwwroot/css/fse.css"));
    }

    [Theory]
    [InlineData("color-scheme: light only")]
    [InlineData("--bs-body-color: #2b2b2b")]
    [InlineData("--bs-body-bg: #f4f7f2")]
    [InlineData("--bs-secondary-color: #4d4d4d")]
    [InlineData("--bs-emphasis-color: #123f0a")]
    [InlineData(".text-body-secondary")]
    [InlineData(".text-muted")]
    [InlineData(".form-select option")]
    [InlineData("--bs-list-group-bg: #ffffff")]
    [InlineData("--bs-card-bg: #ffffff")]
    [InlineData("--bs-table-bg: #ffffff")]
    [InlineData(".btn-outline-secondary")]
    [InlineData(".form-switch .form-check-input:not(:checked)")]
    public void Skin_KeepsTextReadable(string marker)
    {
        Assert.Contains(marker, Read("wwwroot/css/fse.css"));
    }

    [Fact]
    public void WebManifest_UsesFseBranding()
    {
        var manifest = ReadJson("wwwroot/site.webmanifest");

        Assert.Equal("Fényeslitke Sportegyesület", manifest.GetProperty("name").GetString());
        Assert.Equal("FSE", manifest.GetProperty("short_name").GetString());
        Assert.Equal("#195d10", manifest.GetProperty("theme_color").GetString());
    }

    [Fact]
    public void FaviconSvg_IsTheClubCrest()
    {
        var svg = XElement.Parse(Read("wwwroot/favicon.svg"));

        Assert.Equal("svg", svg.Name.LocalName);
        Assert.Equal("0 0 200 200", svg.Attribute("viewBox")?.Value);
        Assert.Contains("#E30613", Read("wwwroot/favicon.svg"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_IsRegisteredInTheSolution()
    {
        var solution = File.ReadAllText(Path.Combine(SolutionDirectory().FullName, "Behind.slnx"));

        Assert.Contains("src/Sites/Behind.Sites.FSE/Behind.Sites.FSE.csproj", solution);
        Assert.Contains("src/Sites/Behind.Sites.FSE.Database.Sqlite/Behind.Sites.FSE.Database.Sqlite.csproj", solution);
        Assert.Contains("src/Sites/Behind.Sites.FSE.Database.Postgres/Behind.Sites.FSE.Database.Postgres.csproj", solution);
        Assert.Contains("src/Sites/Behind.Sites.FSE.Database.Migrator/Behind.Sites.FSE.Database.Migrator.csproj", solution);
    }

    [Fact]
    public void Project_LoadsTheSportModule()
    {
        var project = XDocument.Parse(Read("Behind.Sites.FSE.csproj"));
        var references = project.Descendants("ProjectReference").Select(item => (string?)item.Attribute("Include")).ToList();
        var extensions = Read("WebApiExtensions.cs");

        Assert.Contains(references, reference => reference!.EndsWith("Behind.Sport.WebApi.csproj"));
        Assert.Contains("AddSport()", extensions);
        Assert.Contains("UseSport()", extensions);
    }

    [Fact]
    public void Migrator_UsesTheDatabaseAndDataDirectoryOfTheSite()
    {
        var directory = Path.Combine(SolutionDirectory().FullName, "src", "Sites", "Behind.Sites.FSE.Database.Migrator");
        using var database = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "database.json")));
        using var backend = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "backend.json")));

        Assert.Equal(
            ReadJson("database.json").GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString(),
            database.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
        Assert.Equal(
            ReadJson("backend.json").GetProperty("Backend").GetProperty("DataDirectory").GetString(),
            backend.RootElement.GetProperty("Backend").GetProperty("DataDirectory").GetString());
    }

    [Fact]
    public void FrontendJson_UsesFseBranding()
    {
        var site = ReadJson("frontend.json").GetProperty("Frontend").GetProperty("Site");

        Assert.Equal("Fényeslitke Sportegyesület", site.GetProperty("Title").GetString());
        Assert.Equal("hu-HU", site.GetProperty("DefaultLocale").GetString());
    }

    [Fact]
    public void DatabaseJson_UsesItsOwnDatabase()
    {
        var connection = ReadJson("database.json")
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        Assert.Contains("fse.db", connection);
    }

    [Fact]
    public void BackendJson_UsesItsOwnDataDirectory()
    {
        var dataDirectory = ReadJson("backend.json")
            .GetProperty("Backend")
            .GetProperty("DataDirectory")
            .GetString();

        Assert.Contains("fse", dataDirectory);
    }
}
