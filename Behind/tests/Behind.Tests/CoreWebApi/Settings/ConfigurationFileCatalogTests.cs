using Behind.Core.WebApi.Settings;

namespace Behind.Tests.CoreWebApi.Settings;

public sealed class ConfigurationFileCatalogTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void Files_ListsOnlyTheKnownFilesThatExist()
    {
        File.WriteAllText(Path.Combine(_directory, "security.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "backend.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "unknown.json"), "{}");

        var names = ConfigurationFileCatalog.For(_directory).Files.Select(file => file.Name);

        Assert.Equal(["backend.json", "security.json"], names);
    }

    [Fact]
    public void Find_IgnoresTheCaseOfTheName()
    {
        File.WriteAllText(Path.Combine(_directory, "security.json"), "{}");

        var found = ConfigurationFileCatalog.For(_directory).Find("SECURITY.JSON");

        Assert.Equal("security.json", found.Match(() => string.Empty, file => file.Name));
    }

    [Theory]
    [InlineData("unknown.json")]
    [InlineData("../security.json")]
    [InlineData("security.json/../../etc/passwd")]
    [InlineData("missing.json")]
    public void Find_RefusesAnythingOutsideTheCatalog(string name)
    {
        File.WriteAllText(Path.Combine(_directory, "unknown.json"), "{}");

        Assert.Equal(None, ConfigurationFileCatalog.For(_directory).Find(name));
    }

    [Fact]
    public void For_WithModuleNames_ListsTheModuleFilesThatExist()
    {
        File.WriteAllText(Path.Combine(_directory, "backend.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "notes.json"), "{}");

        var catalog = ConfigurationFileCatalog.For(_directory, ["notes.json", "missing.json", "BACKEND.json"]);

        Assert.Equal(["backend.json", "notes.json"], catalog.Files.Select(file => file.Name));
        Assert.Equal(Some("notes.json"), catalog.Find("notes.json").Map(file => file.Name));
    }

    [Fact]
    public void DefaultNames_CoverEveryConfigurationFileOfTheApplication()
    {
        Assert.Equal(
            ["appsettings.json", "backend.json", "database.json", "email.json", "frontend.json", "security.json"],
            ConfigurationFileCatalog.DefaultNames);
    }
}
