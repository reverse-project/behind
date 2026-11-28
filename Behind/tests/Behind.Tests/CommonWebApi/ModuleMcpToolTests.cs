using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Behind.Common.WebApi.Mcp;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing.Patterns;
using Json.Schema;

namespace Behind.Tests.CommonWebApi;

public sealed class ModuleMcpToolTests
{
    public sealed record NestedRequest(string Name, NestedRequest? Child = null);

    private static ModuleMcpTool Tool()
    {
        var route = RoutePatternFactory.Parse("/module/documents/{**path}");
        return new ModuleMcpTool("module_post_documents_by_path", "Save a document", "POST",
            "/module/documents/{path}", route.Parameters,
            [
                new() { Name = "path", Type = typeof(string), Source = BindingSource.Path, IsRequired = true },
                new() { Name = "published", Type = typeof(bool), Source = BindingSource.Query },
                new() { Name = "request", Type = typeof(NestedRequest), Source = BindingSource.Body, IsRequired = true }
            ], new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });
    }

    [Fact]
    public void NestedBodyReferencesResolveWithinTheToolInputSchema()
    {
        var schema = Tool().Definition()["inputSchema"]!;
        Assert.Contains("body", schema["required"]!.AsArray().Select(value => value!.GetValue<string>()));
        var validator = JsonSchema.FromText(schema.ToJsonString());
        Assert.True(validator.Evaluate(JsonNode.Parse("""
            {"route":{"path":"guide"},"body":{"name":"root","child":{"name":"child"}}}
            """)).IsValid);
        Assert.False(validator.Evaluate(JsonNode.Parse("""
            {"route":{"path":"guide"},"body":{"name":"root","child":{"name":42}}}
            """)).IsValid);
    }

    [Theory]
    [InlineData("guide/start", true)]
    [InlineData("guide/../mcp", false)]
    [InlineData("guide/%2e%2e/mcp", false)]
    [InlineData("guide?target=/mcp", false)]
    [InlineData("{path}", false)]
    public void CatchAllRoutesPreserveSafeSubpathsAndTypedQueryValues(string path, bool valid)
    {
        using var arguments = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            route = new { path }, query = new { published = true }, body = new { name = "Document" }
        }));
        var bound = Tool().Bind(arguments.RootElement);
        Assert.Equal(valid, bound.Match(() => false, _ => true));
        if (valid)
        {
            var request = bound.Match(() => throw new InvalidOperationException(), value => value);
            Assert.Equal("/module/documents/" + path, request["path"]!.GetValue<string>());
            Assert.Equal("true", request["query"]!["published"]!.GetValue<string>());
            Assert.Equal("Document", request["body"]!["name"]!.GetValue<string>());
        }
    }
}
