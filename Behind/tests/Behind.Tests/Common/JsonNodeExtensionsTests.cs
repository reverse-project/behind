using System.Text.Json.Nodes;
using Behind.Common.Configuration;

namespace Behind.Tests.Common;

public class JsonNodeExtensionsTests
{
    [Fact]
    public void WithoutNulls_RemovesNullPropertiesAtEveryDepth()
    {
        var node = JsonNode.Parse("""{ "A": null, "B": { "C": null, "D": 1 }, "E": [ { "F": null, "G": 2 }, null ] }""")!;

        Assert.Equal("""{"B":{"D":1},"E":[{"G":2},null]}""", node.WithoutNulls().ToJsonString());
    }

    [Fact]
    public void WithoutNulls_KeepsAValueWithoutNulls()
    {
        Assert.Equal("""{"A":"x"}""", JsonNode.Parse("""{ "A": "x" }""")!.WithoutNulls().ToJsonString());
    }
}
