using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Behind.Common;
using Behind.Core.Tools;
using Json.Schema;

namespace Behind.Tests.Common;

public sealed class BundledJsonSchemaTests
{
    private const string Changelog = "changelog-1.0.0.schema.json";
    private const string Licenses = "licenses-1.0.0.schema.json";
    private const string Locale = "locale-1.0.0.schema.json";

    private const string Release = """
        {"version":"next-preview","date":"release pending","changes":[]}
        """;

    private const string License = """
        {"name":"Example","version":"rolling","license":"MIT","copyright":"Contributors",
         "url":"https://example.org/","scope":"frontend","text":"licenses/MIT.txt"}
        """;

    [Theory]
    [InlineData(Changelog, @"^changelog\.json$")]
    [InlineData(Licenses, @"^licenses\.json$")]
    [InlineData(Locale, @"^(modules/[^/]+/)?locales/[^/]+\.json$")]
    public void Schema_AcceptsEveryBundledFile(string schemaName, string pattern)
    {
        var schema = ReadSchema(schemaName);
        var files = FrontendDirectories()
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
                .Where(file => Regex.IsMatch(
                    Path.GetRelativePath(directory, file).Replace('\\', '/'), pattern)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            Assert.True(schema.Evaluate(JsonNode.Parse(File.ReadAllText(file))).IsValid, file);
        }
    }

    [Theory]
    [InlineData(Changelog, "null")]
    [InlineData(Changelog, "{}")]
    [InlineData(Changelog, "[]")]
    [InlineData(Changelog, "[null]")]
    [InlineData(Licenses, "null")]
    [InlineData(Licenses, "{}")]
    [InlineData(Licenses, "[]")]
    [InlineData(Licenses, "[null]")]
    [InlineData(Locale, "null")]
    [InlineData(Locale, "[]")]
    [InlineData(Locale, "{}")]
    [InlineData(Locale, """{"messages":null}""")]
    [InlineData(Locale, """{"messages":[]}""")]
    [InlineData(Locale, """{"messages":"translation"}""")]
    [InlineData(Locale, """{"messages":{"key":null}}""")]
    [InlineData(Locale, """{"messages":{"key":1}}""")]
    [InlineData(Locale, """{"messages":{"key":true}}""")]
    [InlineData(Locale, """{"messages":{"key":{}}}""")]
    [InlineData(Locale, """{"messages":{"key":[]}}""")]
    [InlineData(Locale, """{"name":1,"messages":{}}""")]
    [InlineData(Locale, """{"flag":[],"messages":{}}""")]
    public void Schema_RejectsMalformedStructures(string schemaName, string json)
    {
        Assert.False(ReadSchema(schemaName).Evaluate(JsonNode.Parse(json)).IsValid);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("date")]
    [InlineData("changes")]
    public void Changelog_RequiresTypedReleaseFields(string property)
    {
        AssertInvalidProperty(Changelog, JsonNode.Parse(Release)!.AsObject(), property);
    }

    [Theory]
    [InlineData("type")]
    [InlineData("text")]
    public void Changelog_RequiresNonBlankChangeFields(string property)
    {
        var release = JsonNode.Parse(Release)!;
        release["changes"] = JsonNode.Parse("""[{"type":"added","text":"A change"}]""");
        var schema = ReadSchema(Changelog);
        var change = release["changes"]![0]!.AsObject();

        foreach (var invalid in new JsonNode?[] { null, JsonValue.Create(3), JsonValue.Create(" \t\r\n") })
        {
            change[property] = invalid;
            Assert.False(schema.Evaluate(new JsonArray(release.DeepClone())).IsValid, property);
        }

        change.Remove(property);
        Assert.False(schema.Evaluate(new JsonArray(release.DeepClone())).IsValid, property);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[3]")]
    [InlineData("""["added"]""")]
    public void Changelog_RejectsMalformedChangeCollections(string json)
    {
        var release = JsonNode.Parse(Release)!;
        release["changes"] = JsonNode.Parse(json);

        Assert.False(ReadSchema(Changelog).Evaluate(new JsonArray(release)).IsValid);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("version")]
    [InlineData("license")]
    [InlineData("copyright")]
    [InlineData("url")]
    [InlineData("scope")]
    [InlineData("text")]
    public void Licenses_RequiresCompleteNonBlankRecords(string property)
    {
        AssertInvalidProperty(Licenses, JsonNode.Parse(License)!.AsObject(), property);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("Frontend")]
    public void Licenses_RejectsUnknownScopes(string scope)
    {
        var license = JsonNode.Parse(License)!;
        license["scope"] = scope;

        Assert.False(ReadSchema(Licenses).Evaluate(new JsonArray(license)).IsValid);
    }

    [Fact]
    public void Licenses_RejectsNonTextNotes()
    {
        var license = JsonNode.Parse(License)!;
        license["note"] = 1;

        Assert.False(ReadSchema(Licenses).Evaluate(new JsonArray(license)).IsValid);
    }

    [Fact]
    public void Changelog_PreservesReleaseNotesGeneratorSupportedForms()
    {
        var release = JsonNode.Parse(Release)!;
        release["metadata"] = new JsonObject();
        var empty = new JsonArray(release.DeepClone()).ToJsonString();

        Assert.True(ReadSchema(Changelog).Evaluate(JsonNode.Parse(empty)).IsValid);
        Assert.Contains("next-preview", ReleaseNotesGenerator.Generate(empty));

        release["changes"] = JsonNode.Parse("""
            [{"type":"deprecated","text":"First line\nSecond | line","metadata":true}]
            """);
        var json = new JsonArray(release).ToJsonString();

        Assert.True(ReadSchema(Changelog).Evaluate(JsonNode.Parse(json)).IsValid);
        Assert.Contains("| Deprecated |", ReleaseNotesGenerator.Generate(json));
    }

    [Theory]
    [InlineData("""{"messages":{}}""")]
    [InlineData("""{"name":"  ","messages":{}}""")]
    [InlineData("""{"name":null,"flag":null,"messages":{}}""")]
    [InlineData("""{"flag":"","messages":{"arbitrary.key":"","template":"Hello {0}\n{1}"}}""")]
    [InlineData("""{"name":"Magyar","flag":"🇭🇺","metadata":true,"messages":{"key":"Árvíztűrő"}}""")]
    public void Locale_PreservesCatalogFallbacksAndStringDictionaryForms(string json)
    {
        Assert.True(ReadSchema(Locale).Evaluate(JsonNode.Parse(json)).IsValid);
        using var document = JsonDocument.Parse(json);
        Assert.NotNull(document.RootElement.GetProperty("messages").Deserialize<Dictionary<string, string>>());
    }

    [Theory]
    [InlineData("frontend")]
    [InlineData("server")]
    [InlineData("development")]
    public void Licenses_PreservesScopesOptionalNotesAndAdditionalMetadata(string scope)
    {
        var license = JsonNode.Parse(License)!;
        license["scope"] = scope;
        license["metadata"] = true;
        var schema = ReadSchema(Licenses);

        foreach (var note in new string?[] { null, "", "First line\nSecond line" })
        {
            license["note"] = note;
            Assert.True(schema.Evaluate(new JsonArray(license.DeepClone())).IsValid);
        }
    }

    private static void AssertInvalidProperty(string schemaName, JsonObject record, string property)
    {
        var schema = ReadSchema(schemaName);
        foreach (var invalid in new JsonNode?[] { null, JsonValue.Create(3), JsonValue.Create(" \t\r\n") })
        {
            record[property] = invalid;
            Assert.False(schema.Evaluate(new JsonArray(record.DeepClone())).IsValid, property);
        }

        record.Remove(property);
        Assert.False(schema.Evaluate(new JsonArray(record.DeepClone())).IsValid, property);
    }

    private static JsonSchema ReadSchema(string fileName)
    {
        var assembly = typeof(FunctionalExtensions).Assembly;
        var resource = Assert.Single(assembly.GetManifestResourceNames(),
            name => name.EndsWith($".{fileName}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private static IEnumerable<string> FrontendDirectories()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Directory.EnumerateDirectories(Path.Combine(directory.FullName, "src"))
            .SelectMany(Directory.EnumerateDirectories)
            .Select(project => Path.Combine(project, "Frontend"))
            .Where(Directory.Exists);
    }
}
