using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class ChangelogFrontendTests
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
    [InlineData("changelog.json")]
    [InlineData("models/about.js")]
    [InlineData("controllers/changelog.js")]
    [InlineData("viewmodels/changelog.js")]
    [InlineData("viewmodels/footer.js")]
    [InlineData("views/changelog.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Changelog_StartsWithTheCurrentVersion()
    {
        using var document = JsonDocument.Parse(Read("changelog.json"));

        var latest = document.RootElement.EnumerateArray().First();

        Assert.Equal(typeof(ICoreWebApi).Assembly.ProductVersion, latest.GetProperty("version").GetString());
        Assert.True(latest.GetProperty("changes").GetArrayLength() > 0);
    }

    [Fact]
    public void Changelog_ListsEveryVersionOnlyOnce()
    {
        using var document = JsonDocument.Parse(Read("changelog.json"));

        var versions = document.RootElement.EnumerateArray()
            .Select(release => release.GetProperty("version").GetString())
            .ToList();

        Assert.Equal(versions.Distinct(), versions);
    }

    [Fact]
    public void Index_ShowsTheNameAndVersionInTheFooterLinkedToTheChangelog()
    {
        var index = Read("index.html");
        var app = Read("app.js");

        Assert.Contains("<footer id=\"footer-container\"", index);
        Assert.Contains("href=\"#/changelog\"", index);
        Assert.Contains("text: name", index);
        Assert.Contains("text: label", index);
        Assert.Contains("\"#/changelog\": \"controllers/changelog\"", app);
        Assert.Contains("$(\"#footer-container\")[0]", app);
    }

    [Fact]
    public void Footer_LoadsTheVersionFromTheAboutEndpoint()
    {
        var about = Read("models/about.js");
        var footer = Read("viewmodels/footer.js");

        Assert.Contains("load(\"/about\"", about);
        Assert.Contains("about.loadVersion()", footer);
        Assert.Contains("\"v\" + self.version()", footer);
    }

    [Fact]
    public void ChangelogView_ListsReleasesWithTheirChanges()
    {
        var view = Read("views/changelog.html");

        Assert.Contains("foreach: releases", view);
        Assert.Contains("foreach: changes", view);
        Assert.Contains("text: 'v' + version", view);
        Assert.DoesNotContain("<!-- ko", view);
    }

    [Fact]
    public void Branding_ShowsTheConfiguredTitleAndCopyright()
    {
        var index = Read("index.html");
        var branding = Read("models/branding.js");
        var app = Read("app.js");

        Assert.Contains("data-bind=\"text: title\"", index);
        Assert.Contains("visible: hasCopyright, text: copyright", index);
        Assert.Contains("about.loadSite()", branding);
        Assert.Contains("document.title = title", app);
        Assert.Contains("branding.load()", app);
        Assert.Contains("self.title = branding.title", Read("viewmodels/header.js"));
        Assert.Contains("self.copyright = branding.copyright", Read("viewmodels/footer.js"));
    }
}
