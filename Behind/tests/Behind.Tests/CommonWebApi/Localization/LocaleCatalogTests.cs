using Behind.Common.WebApi.Infrastructure.Localization;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CommonWebApi.Localization;

public class LocaleCatalogTests
{
    private static string Locale(string name) => $$"""{ "name": "{{name}}", "messages": { "a": "b" } }""";

    private static LocaleCatalog Catalog(TempFrontendDirectory directory) => new(directory.Provider);

    [Fact]
    public async Task ListAsync_ReturnsTheLocalesWithTheirOfficialNamesOrderedByName()
    {
        using var directory = new TempFrontendDirectory(
            new Dictionary<string, string>
            {
                ["locales/hu-HU.json"] = Locale("Magyar"),
                ["locales/en-US.json"] = Locale("English (International)"),
                ["locales/de-DE.json"] = Locale("Deutsch")
            });

        var locales = await Catalog(directory).ListAsync();

        Assert.Equal(
            [
                new LocaleInfo("de-DE", "Deutsch"),
                new LocaleInfo("en-US", "English (International)"),
                new LocaleInfo("hu-HU", "Magyar")
            ],
            locales);
    }

    [Fact]
    public async Task ListAsync_UsesTheCodeWhenTheNameIsMissingOrBlank()
    {
        using var directory = new TempFrontendDirectory(
            new Dictionary<string, string>
            {
                ["locales/fr-FR.json"] = """{ "messages": {} }""",
                ["locales/it-IT.json"] = """{ "name": "  ", "messages": {} }"""
            });

        var locales = await Catalog(directory).ListAsync();

        Assert.Equal([new LocaleInfo("fr-FR", "fr-FR"), new LocaleInfo("it-IT", "it-IT")], locales);
    }

    [Fact]
    public async Task ListAsync_SkipsInvalidJsonOtherExtensionsAndInvalidCodes()
    {
        using var directory = new TempFrontendDirectory(
            new Dictionary<string, string>
            {
                ["locales/hu-HU.json"] = Locale("Magyar"),
                ["locales/broken.json"] = "{ not json",
                ["locales/Not A Locale.json"] = Locale("Spaces"),
                ["locales/en-US.txt"] = Locale("Text"),
                ["locales/nested/de-DE.json"] = Locale("Nested")
            });

        var locales = await Catalog(directory).ListAsync();

        Assert.Equal([new LocaleInfo("hu-HU", "Magyar")], locales);
    }

    [Fact]
    public async Task ListAsync_WithoutALocalesDirectory_ReturnsNothing()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string>());

        Assert.Empty(await Catalog(directory).ListAsync());
    }

    [Fact]
    public async Task ListAsync_WebRootOverridesAndExtendsTheEmbeddedLocales()
    {
        using var embedded = new TempFrontendDirectory(
            new Dictionary<string, string>
            {
                ["locales/en-US.json"] = Locale("English (International)"),
                ["locales/hu-HU.json"] = Locale("Magyar")
            });
        using var webRoot = new TempFrontendDirectory(
            new Dictionary<string, string>
            {
                ["locales/hu-HU.json"] = Locale("Magyar (testreszabott)"),
                ["locales/de-DE.json"] = Locale("Deutsch")
            });

        var locales = await new LocaleCatalog(new CompositeFileProvider(webRoot.Provider, embedded.Provider)).ListAsync();

        Assert.Equal(
            [
                new LocaleInfo("de-DE", "Deutsch"),
                new LocaleInfo("en-US", "English (International)"),
                new LocaleInfo("hu-HU", "Magyar (testreszabott)")
            ],
            locales);
    }

    [Theory]
    [InlineData("hu-HU", true)]
    [InlineData("en-US", true)]
    [InlineData("fr-FR", false)]
    [InlineData("", false)]
    public async Task ContainsAsync_ReportsWhetherTheLocaleIsAvailable(string code, bool expected)
    {
        using var directory = new TempFrontendDirectory(
            new Dictionary<string, string>
            {
                ["locales/hu-HU.json"] = Locale("Magyar"),
                ["locales/en-US.json"] = Locale("English (International)")
            });

        Assert.Equal(expected, await Catalog(directory).ContainsAsync(code));
    }
}
