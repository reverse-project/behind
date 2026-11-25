using System.Text.Json.Nodes;
using Behind.Common;
using Json.Schema;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountSchemaTests
{
    [Fact]
    public void FamilyRequiresChildManagementCapabilityInVersion320()
    {
        var family = JsonNode.Parse("""
            {
              "accounts": [], "isPrimary": false,
              "primaryUserId": "5418cd9a-dc39-4af9-9845-1891a543eda9",
              "primaryHasPin": false, "canBrowseAndRelease": true,
              "linkUnlinkEnabled": true, "linkUnlinkRequiresDirectSignIn": false,
              "currentHasPin": true, "isLinked": true, "canRespondToInvitations": false,
              "switchAccounts": []
            }
            """)!;
        var schema = ReadDefinition("family", "3.2.0");
        Assert.False(schema.Evaluate(family).IsValid);
        family["canManageChildren"] = "true";
        Assert.False(schema.Evaluate(family).IsValid);
        family["canManageChildren"] = true;
        Assert.True(schema.Evaluate(family).IsValid);
        family["canManageChildren"] = false;
        Assert.True(schema.Evaluate(family).IsValid);
    }

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

    [Fact]
    public void StoredBlankAccountNamesAreAllowedOnlyInResponses()
    {
        var account = JsonNode.Parse("""
            {
              "displayName": "",
              "isChild": true,
              "userId": "286a5ce1-e229-4dc7-b33b-5f8379455d97",
              "hasPin": false,
              "canRelease": false
            }
            """)!;
        var request = JsonNode.Parse("""{"displayName":""}""")!;

        Assert.True(ReadDefinition("account", "1.2.0").Evaluate(account).IsValid);
        Assert.False(ReadDefinition("create", "1.2.0").Evaluate(request).IsValid);
        Assert.False(ReadDefinition("update", "1.2.0").Evaluate(request).IsValid);
    }

    [Theory]
    [InlineData("create", """{"displayName":"Alex","proof":null}""", true)]
    [InlineData("create", """{"displayName":"Alex","pin":"0042","proof":null}""", false)]
    [InlineData("create", """{"displayName":"Alex","pin":"0042","proof":{"currentPin":"0017"}}""", true)]
    [InlineData("create", """{"displayName":"Alex","pin":"0042","proof":{"currentPin":"0017","primaryPin":null}}""", true)]
    [InlineData("create", """{"displayName":"Alex","pin":"0042","proof":{"currentPin":"0017","primaryPin":"0022"}}""", false)]
    [InlineData("update", """{"displayName":"Alex","proof":null}""", true)]
    [InlineData("update", """{"displayName":"Alex","clearPin":true,"proof":null}""", false)]
    [InlineData("update", """{"displayName":"Alex","clearPin":true,"proof":{"currentPin":"0017"}}""", true)]
    public void PinProofMayBeNullOnlyWhenTheRequestDoesNotChangeAPin(string definition, string request, bool expected)
    {
        Assert.Equal(expected, ReadDefinition(definition, "3.1.0").Evaluate(JsonNode.Parse(request)).IsValid);
    }

    [Theory]
    [InlineData("""{"pin":"0042","proof":{"currentPin":"0017","password":null}}""", true)]
    [InlineData("""{"pin":"0042","proof":{"currentPin":"0017","password":"secret"}}""", false)]
    [InlineData("""{"pin":"0042","proof":{"currentPin":null,"password":null}}""", false)]
    [InlineData("""{"pin":"0042","proof":{"currentPin":"0017","primaryPin":null}}""", true)]
    [InlineData("""{"pin":"0042","proof":{"currentPin":"0017","primaryPin":"0022"}}""", false)]
    public void ProofAcceptsExactlyOneNonNullCredential(string request, bool expected)
    {
        Assert.Equal(expected, ReadDefinition("pinMutation", "3.1.0").Evaluate(JsonNode.Parse(request)).IsValid);
    }

    [Fact]
    public void SwitchTargetsHidePrivateDataAndVersion30RequiresTheirPin()
    {
        var family = JsonNode.Parse("""
            {
              "accounts": [{
                "displayName":"Adult",
                "birthday":"1980-01-01",
                "email":"adult@example.test",
                "isChild":false,
                "userId":"5418cd9a-dc39-4af9-9845-1891a543eda9",
                "hasPin":false,
                "canRelease":false
              }],
              "isPrimary":true,
              "primaryUserId":"5418cd9a-dc39-4af9-9845-1891a543eda9",
              "primaryHasPin":false,
              "canBrowseAndRelease":true,
              "linkUnlinkEnabled":true,
              "linkUnlinkRequiresDirectSignIn":false,
              "currentHasPin":false,
              "isLinked":false,
              "canRespondToInvitations":false,
              "switchAccounts": [{
                "displayName":"Target",
                "isChild":false,
                "userId":"286a5ce1-e229-4dc7-b33b-5f8379455d97",
                "hasPin":true
              }]
            }
            """)!;

        Assert.True(ReadDefinition("family", "3.1.0").Evaluate(family).IsValid);
        Assert.True(ReadDefinition("family", "3.0.0").Evaluate(family).IsValid);
        family["switchAccounts"]![0]!["email"] = "private@example.test";
        Assert.False(ReadDefinition("family", "3.1.0").Evaluate(family).IsValid);
        Assert.False(ReadDefinition("family", "3.0.0").Evaluate(family).IsValid);
        family["switchAccounts"]![0]!["email"] = null;
        family["switchAccounts"]![0]!["birthday"] = "1980-01-01";
        Assert.False(ReadDefinition("family", "3.1.0").Evaluate(family).IsValid);
        Assert.False(ReadDefinition("family", "3.0.0").Evaluate(family).IsValid);

        family["switchAccounts"]![0]!["birthday"] = null;
        family["switchAccounts"]![0]!["hasPin"] = false;
        Assert.False(ReadDefinition("family", "3.0.0").Evaluate(family).IsValid);
        family["switchAccounts"]![0]!["hasPin"] = true;
        Assert.True(ReadDefinition("family", "3.0.0").Evaluate(family).IsValid);
    }

    [Theory]
    [InlineData("update", """{"displayName":"Family","clearPin":true,"proof":{"primaryPin":"1234"}}""", true)]
    [InlineData("pinMutation", """{"pin":"5678","proof":{"primaryPin":"1234"}}""", true)]
    [InlineData("pinMutation", """{"clearPin":true,"proof":{"primaryPin":"1234","currentPin":"5678"}}""", false)]
    [InlineData("create", """{"displayName":"Family","pin":"5678","proof":{"primaryPin":"1234"}}""", false)]
    [InlineData("create", """{"displayName":"Family","primaryPin":"5678","proof":{"currentPin":"1234"}}""", true)]
    public void PrimaryPinProof_IsLimitedToPinMutations(string definition, string request, bool expected)
    {
        Assert.Equal(expected, ReadDefinition(definition, "2.1.0").Evaluate(JsonNode.Parse(request)).IsValid);
    }

    [Fact]
    public void Family_RequiresDirectSignInGuidanceFlag()
    {
        var family = JsonNode.Parse("""
            {"isPrimary":true,"primaryUserId":"5418cd9a-dc39-4af9-9845-1891a543eda9","primaryHasPin":true,"linkUnlinkEnabled":false,"currentHasPin":true,"accounts":[]}
            """)!;
        var schema = ReadDefinition("family", "2.4.0");
        Assert.False(schema.Evaluate(family).IsValid);
        family["linkUnlinkRequiresDirectSignIn"] = "true";
        Assert.False(schema.Evaluate(family).IsValid);
        family["linkUnlinkRequiresDirectSignIn"] = true;
        Assert.True(schema.Evaluate(family).IsValid);
        family["linkUnlinkRequiresDirectSignIn"] = false;
        Assert.True(schema.Evaluate(family).IsValid);
    }

    [Fact]
    public void Family_RequiresBrowseAndReleaseCapability()
    {
        var family = JsonNode.Parse("""
            {"isPrimary":true,"primaryUserId":"5418cd9a-dc39-4af9-9845-1891a543eda9","primaryHasPin":true,"linkUnlinkEnabled":false,"linkUnlinkRequiresDirectSignIn":true,"currentHasPin":true,"accounts":[]}
            """)!;
        var schema = ReadDefinition("family", "2.5.0");
        Assert.False(schema.Evaluate(family).IsValid);
        family["canBrowseAndRelease"] = "true";
        Assert.False(schema.Evaluate(family).IsValid);
        family["canBrowseAndRelease"] = true;
        Assert.True(schema.Evaluate(family).IsValid);
        family["canBrowseAndRelease"] = false;
        Assert.True(schema.Evaluate(family).IsValid);
    }

    [Fact]
    public void Family_RequiresCurrentAccountPinStatus()
    {
        var family = JsonNode.Parse("""
            {"isPrimary":false,"primaryUserId":"5418cd9a-dc39-4af9-9845-1891a543eda9","primaryHasPin":false,"linkUnlinkEnabled":false,"accounts":[]}
            """)!;
        var schema = ReadDefinition("family", "2.2.0");
        Assert.False(schema.Evaluate(family).IsValid);
        family["currentHasPin"] = "true";
        Assert.False(schema.Evaluate(family).IsValid);
        family["currentHasPin"] = true;
        Assert.True(schema.Evaluate(family).IsValid);
        family["currentHasPin"] = false;
        Assert.True(schema.Evaluate(family).IsValid);
    }

    [Theory]
    [InlineData("""{"pin":"0042","proof":{"currentPin":"0017"}}""", true)]
    [InlineData("""{"clearPin":true,"proof":{"currentPin":"0017"}}""", true)]
    [InlineData("""{"pin":"0042"}""", false)]
    [InlineData("""{"pin":"0042","proof":{"currentPin":""}}""", false)]
    [InlineData("""{"pin":"0042","proof":{"currentPin":"0017","primaryPin":"0035"}}""", false)]
    [InlineData("""{"pin":"0042","proof":{"primaryPin":"0035"}}""", false)]
    [InlineData("""{"pin":"0042","proof":{"password":"secret"}}""", false)]
    [InlineData("""{"pin":"0042","proof":{"magicToken":"token"}}""", false)]
    public void SelfPinMutation_RequiresOnlyTheCurrentPin(string request, bool expected)
    {
        Assert.Equal(expected, ReadDefinition("selfPinMutation", "2.2.0").Evaluate(JsonNode.Parse(request)).IsValid);
    }

    [Theory]
    [InlineData("""{"pin":"0042"}""", true)]
    [InlineData("""{"pin":"0042","clearPin":false,"proof":null}""", true)]
    [InlineData("""{"pin":"0042","proof":{"currentPin":"0017"}}""", true)]
    [InlineData("""{"clearPin":true,"proof":{"currentPin":"0017"}}""", true)]
    [InlineData("""{"clearPin":true}""", false)]
    [InlineData("""{"clearPin":true,"pin":"0042"}""", false)]
    [InlineData("""{"pin":""}""", false)]
    [InlineData("""{"pin":"invalid"}""", false)]
    [InlineData("""{"pin":"0042","proof":{}}""", false)]
    [InlineData("""{"pin":"0042","proof":{"password":"secret"}}""", false)]
    public void SelfPinMutation_AllowsSetupWithoutProof(string request, bool expected)
    {
        Assert.Equal(expected, ReadDefinition("selfPinMutation", "2.3.0").Evaluate(JsonNode.Parse(request)).IsValid);
    }

    [Theory]
    [InlineData("{}", false)]
    [InlineData("""{"pin":null}""", false)]
    [InlineData("""{"pin":"0001"}""", true)]
    public void SwitchRequiresDestinationPin(string request, bool expected)
    {
        Assert.Equal(expected, ReadDefinition("switch", "3.0.0").Evaluate(JsonNode.Parse(request)).IsValid);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"pin":null}""")]
    [InlineData("""{"pin":""}""")]
    [InlineData("""{"pin":"0001"}""")]
    public void SwitchAllowsPrimaryRequestsWithoutDestinationPin(string request)
    {
        Assert.True(ReadDefinition("switch", "3.1.0").Evaluate(JsonNode.Parse(request)).IsValid);
    }

    [Fact]
    public void FamilyRequiresIndependentSwitchTargetsAndInvitationResponseCapability()
    {
        var family = JsonNode.Parse("""
            {"isPrimary":false,"primaryUserId":"5418cd9a-dc39-4af9-9845-1891a543eda9","primaryHasPin":false,
            "linkUnlinkEnabled":false,"linkUnlinkRequiresDirectSignIn":false,"canBrowseAndRelease":false,
            "currentHasPin":true,"accounts":[],"isLinked":true,"canRespondToInvitations":true}
            """)!;
        var schema = ReadDefinition("family", "3.0.0");
        Assert.False(schema.Evaluate(family).IsValid);
        family["switchAccounts"] = new JsonArray();
        Assert.True(schema.Evaluate(family).IsValid);
        family["canRespondToInvitations"] = null;
        Assert.False(schema.Evaluate(family).IsValid);
    }

    private static JsonSchema ReadDefinition(string name, string version = "1.2.1")
    {
        var assembly = typeof(FunctionalExtensions).Assembly;
        var resource = Assert.Single(assembly.GetManifestResourceNames(),
            item => item.EndsWith($".subaccounts-{version}.schema.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        var schema = JsonNode.Parse(stream)!;
        schema["$ref"] = $"#/$defs/{name}";
        return JsonSchema.FromText(schema.ToJsonString());
    }
}
