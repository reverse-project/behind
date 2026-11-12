using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public partial class LocalizationCompletenessTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static readonly string[] SourceExtensions = [".js", ".html"];

    private static readonly string[] LibraryFolders = ["libraries"];

    private static readonly string[] TechnicalSuffixes = ["json", "js", "html", "css", "txt", "png", "svg"];

    [GeneratedRegex(@"[""']((?:[a-z][A-Za-z0-9]*)(?:\.[A-Za-z0-9]+)+)[""']")]
    private static partial Regex QuotedKeyPattern();

    [GeneratedRegex(@"<(?<tag>[a-zA-Z][a-zA-Z0-9-]*)(?<attributes>(?:[^<>""']|""[^""]*""|'[^']*')*)>(?<text>[^<>]+)")]
    private static partial Regex TextNodePattern();

    private static IEnumerable<string> SourceFiles(string folder = "/")
    {
        foreach (var entry in Provider.GetDirectoryContents(folder))
        {
            var path = folder == "/" ? entry.Name : $"{folder}/{entry.Name}";

            if (entry.IsDirectory)
            {
                if (!LibraryFolders.Contains(entry.Name))
                {
                    foreach (var nested in SourceFiles(path))
                    {
                        yield return nested;
                    }
                }
            }
            else if (SourceExtensions.Contains(Path.GetExtension(entry.Name)))
            {
                yield return path;
            }
        }
    }

    private static string Read(string path)
    {
        using var reader = new StreamReader(Provider.GetFileInfo(path).CreateReadStream(), Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private static IReadOnlyDictionary<string, string> Messages(string locale)
    {
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));

        return document.RootElement.GetProperty("messages")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!);
    }

    private static IEnumerable<string> KeyLikeLiterals(string source, IReadOnlySet<string> prefixes) =>
        QuotedKeyPattern().Matches(source)
            .Select(match => match.Groups[1].Value)
            .Where(literal => prefixes.Contains(literal.Split('.')[0]))
            .Where(literal => !TechnicalSuffixes.Contains(literal.Split('.')[^1]))
            .Distinct();

    private static IReadOnlySet<string> Prefixes() =>
        Messages("en-US").Keys.Select(key => key.Split('.')[0]).ToHashSet();

    [Fact]
    public void EveryKeyUsedInTheFrontend_IsDefinedInTheInternationalEnglishLocale()
    {
        var english = Messages("en-US");
        var prefixes = Prefixes();

        var missing = SourceFiles()
            .SelectMany(path => KeyLikeLiterals(Read(path), prefixes).Select(key => $"{path}: {key}"))
            .Where(entry => !english.ContainsKey(entry[(entry.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryKeyOfTheInternationalEnglishLocale_IsUsedByTheFrontend()
    {
        var english = Messages("en-US");
        var prefixes = Prefixes();

        var used = SourceFiles()
            .SelectMany(path => KeyLikeLiterals(Read(path), prefixes))
            .ToHashSet();
        used.UnionWith(CoreModule.Descriptor.Widgets.Select(widget => widget.Name));

        var unused = english.Keys.Where(key => !used.Contains(key)).Order().ToList();

        Assert.True(unused.Count == 0, string.Join(", ", unused));
    }

    [Fact]
    public void Hungarian_HasExactlyTheKeysOfTheInternationalEnglishLocale()
    {
        Assert.Equal(Messages("en-US").Keys.Order(), Messages("hu-HU").Keys.Order());
    }

    [Fact]
    public void Hungarian_KeepsThePlaceholdersOfTheInternationalEnglishLocale()
    {
        var english = Messages("en-US");

        var differing = Messages("hu-HU")
            .Where(pair => !Placeholders(english[pair.Key]).SequenceEqual(Placeholders(pair.Value)))
            .Select(pair => pair.Key)
            .ToList();

        Assert.Empty(differing);
    }

    [Fact]
    public void Views_HaveNoTextThatIsNotLocalized()
    {
        var offenders = SourceFiles()
            .Where(path => path.EndsWith(".html", StringComparison.Ordinal) && path != "index.html")
            .SelectMany(path => UnlocalizedTexts(Read(path)).Select(text => $"{path}: {text}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("""<button data-bind="enable: count() > 1, t: 'common.remove'">Remove</button>""")]
    [InlineData("""<button data-bind="enable: count() < 2, t: 'common.remove'">Remove</button>""")]
    [InlineData("""<p title="A > B" data-bind="text: message">Message</p>""")]
    [InlineData("""<label title='A < B' data-bind="t: 'common.name'">Name</label>""")]
    public void LocalizedText_WithAngleBracketsInQuotedAttributes_IsNotFlagged(string html)
    {
        Assert.Empty(UnlocalizedTexts(html));
    }

    [Theory]
    [InlineData("""<button data-bind="enable: count() > 1">Remove</button>""", "Remove")]
    [InlineData("""<p title="A < B">Untranslated text</p>""", "Untranslated text")]
    [InlineData("""<button data-bind="enable: count() > 1, t: 'common.remove'">Remove</button><p>Untranslated text</p>""", "Untranslated text")]
    public void UnlocalizedText_WithAngleBracketsInQuotedAttributes_IsStillFlagged(string html, string expected)
    {
        Assert.Equal(expected, Assert.Single(UnlocalizedTexts(html)));
    }

    private static IEnumerable<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).Order();

    private static IEnumerable<string> UnlocalizedTexts(string html) =>
        TextNodePattern().Matches(html)
            .Where(match => match.Groups["tag"].Value is not ("script" or "style" or "option" or "textarea" or "code"))
            .Where(match => HasLetters(match.Groups["text"].Value))
            .Where(match => !IsBound(match.Groups["attributes"].Value))
            .Select(match => match.Groups["text"].Value.Trim());

    private static bool HasLetters(string text) =>
        Regex.IsMatch(Regex.Replace(text, @"&[a-z]+;|\{\{.*?\}\}", string.Empty), @"[A-Za-z]{2,}");

    private static bool IsBound(string attributes) =>
        Regex.IsMatch(attributes, @"data-bind=""[^""]*\b(t|text|html|value|textInput):");
}
