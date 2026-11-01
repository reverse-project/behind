using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class LicensesFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static readonly IReadOnlySet<string> PermissiveLicenses = new HashSet<string>
    {
        "MIT",
        "Apache-2.0",
        "BSD-2-Clause",
        "BSD-3-Clause",
        "Public Domain",
        "CC-BY-4.0 AND OFL-1.1 AND MIT"
    };

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static IReadOnlyList<JsonElement> Licenses()
    {
        using var document = JsonDocument.Parse(Read("licenses.json"));
        return document.RootElement.EnumerateArray().Select(element => element.Clone()).ToList();
    }

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

    private static string SolutionFile(string name) => Path.Combine(SolutionDirectory().FullName, name);

    private static string RepositoryFile(string name) => Path.Combine(SolutionDirectory().Parent!.FullName, name);

    [Theory]
    [InlineData("licenses.json")]
    [InlineData("controllers/licenses.js")]
    [InlineData("viewmodels/licenses.js")]
    [InlineData("views/licenses.html")]
    [InlineData("licenses/Behind.txt")]
    [InlineData("licenses/MIT.txt")]
    [InlineData("licenses/Apache-2.0.txt")]
    [InlineData("licenses/BSD-2-Clause.txt")]
    [InlineData("licenses/BSD-3-Clause.txt")]
    [InlineData("licenses/SQLite.txt")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Footer_LinksToTheLicensesPage()
    {
        var index = Read("index.html");
        var app = Read("app.js");

        var footer = index[index.IndexOf("<footer id=\"footer-container\"", StringComparison.Ordinal)..];

        Assert.Contains("href=\"#/licenses\"", footer);
        Assert.Contains("\"#/licenses\": \"controllers/licenses\"", app);
    }

    [Fact]
    public void About_LoadsTheLicensesFile()
    {
        var about = Read("models/about.js");
        var viewModel = Read("viewmodels/licenses.js");

        Assert.Contains("\"/licenses.json\"", about);
        Assert.Contains("about.loadLicenses()", viewModel);
    }

    [Fact]
    public void LicensesView_ListsTheComponentsWithTheirLicenseAndCopyright()
    {
        var view = Read("views/licenses.html");

        Assert.Contains("foreach: scopes", view);
        Assert.Contains("foreach: licenses", view);
        Assert.Contains("text: copyright", view);
        Assert.Contains("attr: { href: text }", view);
        Assert.DoesNotContain("<!-- ko", view);
    }

    [Fact]
    public void BehindLicense_IsTheGplWithTheProjectNotice()
    {
        var text = Read("licenses/Behind.txt");

        Assert.Contains("SPDX-License-Identifier: GPL-2.0-or-later", text);
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", text);
        Assert.Contains("Version 2, June 1991", text);
        Assert.Contains("END OF TERMS AND CONDITIONS", text);
    }

    [Fact]
    public void BehindLicense_MatchesTheRepositoryLicenseFile()
    {
        Assert.Equal(File.ReadAllText(RepositoryFile("LICENSE")), Read("licenses/Behind.txt"));
    }

    [Fact]
    public void BuildProperties_DeclareTheSameLicenseAsTheViewModel()
    {
        var properties = XDocument.Load(SolutionFile("Directory.Build.props"));
        var expression = properties.Descendants("PackageLicenseExpression").Single().Value;
        var copyright = properties.Descendants("Copyright").Single().Value;
        var viewModel = Read("viewmodels/licenses.js");

        Assert.Equal("GPL-2.0-or-later", expression);
        Assert.Contains($"self.license = \"{expression}\"", viewModel);
        Assert.Contains($"self.copyright = \"{copyright}\"", viewModel);
    }

    [Fact]
    public void Licenses_DescribeEveryComponentCompletely()
    {
        var licenses = Licenses();

        Assert.NotEmpty(licenses);
        Assert.All(licenses, license =>
        {
            foreach (var property in new[] { "name", "version", "license", "copyright", "url", "scope", "text" })
            {
                Assert.False(string.IsNullOrWhiteSpace(license.GetProperty(property).GetString()), property);
            }
        });
    }

    [Fact]
    public void Licenses_ListEveryComponentOnlyOnce()
    {
        var keys = Licenses()
            .Select(license => $"{license.GetProperty("name").GetString()}/{license.GetProperty("version").GetString()}")
            .ToList();

        Assert.Equal(keys.Distinct(StringComparer.OrdinalIgnoreCase), keys);
    }

    [Fact]
    public void Licenses_PointToExistingLicenseTexts()
    {
        Assert.All(Licenses(), license => Assert.True(
            Provider.GetFileInfo(license.GetProperty("text").GetString()!).Exists,
            license.GetProperty("name").GetString()));
    }

    [Fact]
    public void Licenses_UseOnlyKnownScopes()
    {
        var scopes = new[] { "frontend", "server", "development" };

        Assert.All(Licenses(), license => Assert.Contains(license.GetProperty("scope").GetString(), scopes));
    }

    [Fact]
    public void Licenses_AreAllCompatibleWithTheGplOrLaterLicenseOfBehind()
    {
        Assert.All(Licenses(), license => Assert.Contains(license.GetProperty("license").GetString()!, PermissiveLicenses));
    }

    [Fact]
    public void Licenses_ListEveryPackageDeclaredInTheCentralPackageFile()
    {
        var packages = XDocument.Load(SolutionFile("Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Select(package => package.Attribute("Include")!.Value)
            .ToList();
        var listed = Licenses().Select(license => license.GetProperty("name").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(packages);
        Assert.Empty(packages.Where(package => !listed.Contains(package)));
    }
}
