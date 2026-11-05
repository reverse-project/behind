using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;

namespace Behind.Tests.Common;

public sealed class JsonConfigurationFileExtensionsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    private FileInfo FileWith(string content)
    {
        var path = Path.Combine(_directory, "config.json");
        File.WriteAllText(path, content);
        return new FileInfo(path);
    }

    private JsonNode Read(FileInfo file) => JsonNode.Parse(File.ReadAllText(file.FullName))!;

    [Fact]
    public async Task SetJsonValuesAsync_UpdatesExistingValueAndKeepsSiblings()
    {
        var file = FileWith("""{ "A": { "B": "old", "C": "keep" }, "D": 1 }""");

        await file.SetJsonValuesAsync(new Dictionary<string, string?> { ["A:B"] = "new" });

        var json = Read(file);
        Assert.Equal("new", (string?)json["A"]!["B"]);
        Assert.Equal("keep", (string?)json["A"]!["C"]);
        Assert.Equal(1, (int?)json["D"]);
    }

    [Fact]
    public async Task SetJsonValuesAsync_CreatesMissingSections()
    {
        var file = FileWith("{}");

        await file.SetJsonValuesAsync(new Dictionary<string, string?> { ["A:B:C"] = "value" });

        Assert.Equal("value", (string?)Read(file)["A"]!["B"]!["C"]);
    }

    [Fact]
    public async Task SetJsonValuesAsync_NullValueRemovesProperty()
    {
        var file = FileWith("""{ "A": { "B": "old", "C": "keep" } }""");

        await file.SetJsonValuesAsync(new Dictionary<string, string?> { ["A:B"] = null });

        var section = Read(file)["A"]!.AsObject();
        Assert.False(section.ContainsKey("B"));
        Assert.True(section.ContainsKey("C"));
    }

    [Fact]
    public async Task SetJsonValuesAsync_ToleratesCommentsAndTrailingCommas()
    {
        var file = FileWith("""
                            {
                              // comment
                              "A": "x",
                            }
                            """);

        await file.SetJsonValuesAsync(new Dictionary<string, string?> { ["B"] = "y" });

        Assert.Equal("x", (string?)Read(file)["A"]);
        Assert.Equal("y", (string?)Read(file)["B"]);
    }

    [Fact]
    public async Task SetJsonValuesAsync_NonObjectRoot_Throws()
    {
        var file = FileWith("[1]");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            file.SetJsonValuesAsync(new Dictionary<string, string?> { ["A"] = "x" }));
    }

    [Fact]
    public async Task SetJsonNodesAsync_KeepsTheTypesOfTheValues()
    {
        var file = FileWith("""{ "A": { "On": false, "Count": 1 } }""");

        await file.SetJsonNodesAsync(
            new Dictionary<string, JsonNode?> { ["A:On"] = JsonValue.Create(true), ["A:Count"] = JsonValue.Create(7) });

        var section = Read(file)["A"]!;
        Assert.True((bool?)section["On"]);
        Assert.Equal(7, (int?)section["Count"]);
    }

    [Fact]
    public async Task SetJsonNodesAsync_ReplacesAnArrayItem()
    {
        var file = FileWith("""{ "A": { "List": ["x", "y"] } }""");

        await file.SetJsonNodesAsync(new Dictionary<string, JsonNode?> { ["A:List:1"] = JsonValue.Create("z") });

        Assert.Equal(["x", "z"], Read(file)["A"]!["List"]!.AsArray().Select(item => (string?)item));
    }

    [Fact]
    public async Task ReadJsonAsync_ListsEveryLeafWithItsPath()
    {
        var file = FileWith("""{ "A": { "B": "x", "C": [1, { "D": true }] }, "E": null }""");

        var fields = (await file.ReadJsonAsync()).Fields();

        Assert.Equal(["A:B", "A:C:0", "A:C:1:D"], fields.Select(field => field.Path));
    }

    [Fact]
    public async Task FindValue_ReturnsTheLeafOrNothing()
    {
        var root = await FileWith("""{ "A": { "B": "x" } }""").ReadJsonAsync();

        Assert.Equal("x", root.FindValue("A:B")!.GetValue<string>());
        Assert.Null(root.FindValue("A:Missing"));
        Assert.Null(root.FindValue("A"));
        Assert.Null(root.FindValue("Missing:Deeper"));
    }

    [Fact]
    public async Task SetJsonValuesAsync_LeavesNoTemporaryFiles()
    {
        var file = FileWith("{}");

        await file.SetJsonValuesAsync(new Dictionary<string, string?> { ["A"] = "x" });

        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task SetJsonValuesAsync_DirectoryNotWritable_WritesFileInPlace()
    {
        var file = FileWith("""{ "A": "old" }""");
        File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            await file.SetJsonValuesAsync(new Dictionary<string, string?> { ["A"] = "new" });
        }
        finally
        {
            File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.Equal("new", (string?)Read(file)["A"]);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task SetJsonValuesAsync_ConcurrentWrites_KeepFileValid()
    {
        var file = FileWith("{}");

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            file.SetJsonValuesAsync(new Dictionary<string, string?> { [$"K{i}"] = i.ToString() })));

        Assert.Equal(20, Read(file).AsObject().Count);
    }
}
