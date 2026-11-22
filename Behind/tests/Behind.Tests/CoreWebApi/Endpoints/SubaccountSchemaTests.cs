using System.Text.Json.Nodes;
using Behind.Common;
using Json.Schema;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountSchemaTests
{
    [Theory]
    [InlineData("""{"displayName":"Family","clearPin":true,"pin":"1234"}""", false)]
    [InlineData("""{"displayName":"Family","clearPin":true,"pin":""}""", false)]
    [InlineData("""{"displayName":"Family","clearPin":true,"pin":null}""", true)]
    [InlineData("""{"displayName":"Family","clearPin":true}""", true)]
    [InlineData("""{"displayName":"Family","clearPin":false,"pin":"1234"}""", true)]
    [InlineData("""{"displayName":"Family","pin":"1234"}""", true)]
    public void Update_ValidatesPinOperations(string request, bool expected)
    {
        Assert.Equal(expected, ReadDefinition("update").Evaluate(JsonNode.Parse(request)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Family_AcceptsStoredNamesThatCreateAndUpdateReject(string displayName)
    {
        var family = JsonNode.Parse("""
            {
              "isPrimary": true,
              "primaryUserId": "5418cd9a-dc39-4af9-9845-1891a543eda9",
              "primaryHasPin": false,
              "linkUnlinkEnabled": true,
              "accounts": [{
                "userId": "286a5ce1-e229-4dc7-b33b-5f8379455d97",
                "isChild": false,
                "hasPin": false,
                "canRelease": true
              }]
            }
            """)!;
        family["accounts"]![0]!["displayName"] = displayName;
        var request = new JsonObject { ["displayName"] = displayName };

        Assert.True(ReadDefinition("family").Evaluate(family).IsValid);
        Assert.False(ReadDefinition("create").Evaluate(request).IsValid);
        Assert.False(ReadDefinition("update").Evaluate(request).IsValid);
    }

    private static JsonSchema ReadDefinition(string name)
    {
        var assembly = typeof(FunctionalExtensions).Assembly;
        var resource = Assert.Single(assembly.GetManifestResourceNames(),
            item => item.EndsWith(".subaccounts-1.2.1.schema.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        var schema = JsonNode.Parse(stream)!;
        schema["$ref"] = $"#/$defs/{name}";
        return JsonSchema.FromText(schema.ToJsonString());
    }
}
