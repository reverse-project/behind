using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Settings;

namespace Behind.Tests.CoreWebApi.Settings;

public sealed class SettingsChangeExtensionsTests
{
    private const string Security = "security.json";

    private static readonly JsonObject Root = JsonNode.Parse(
        """
        {
          "Security": {
            "SecretKey": "totp://ABCDEFGH",
            "Jwt": { "Issuer": "me", "SigningKey": "signing-key-value" },
            "Elevation": { "Enabled": true },
            "MagicLink": { "ActualSecret": "uuid://x", "RequestLimit": 5 }
          },
          "ConnectionStrings": { "Default": "Data Source=a.db" }
        }
        """)!.AsObject();

    private static IReadOnlyDictionary<string, JsonElement> Values(params (string Path, string Json)[] pairs) =>
        pairs.ToDictionary(pair => pair.Path, pair => JsonDocument.Parse(pair.Json).RootElement.Clone());

    private static IReadOnlyDictionary<string, JsonNode?> Planned(string file, params (string Path, string Json)[] pairs) =>
        Root.PlanChanges(file, Values(pairs)).Match(
            () => throw new InvalidOperationException("The change was refused."),
            planned => planned);

    [Fact]
    public void PlanChanges_AcceptsValuesOfTheSameType()
    {
        var planned = Planned(Security, ("Security:Jwt:Issuer", "\"you\""), ("Security:Elevation:Enabled", "false"), ("Security:MagicLink:RequestLimit", "9"));

        Assert.Equal("you", (string?)planned["Security:Jwt:Issuer"]);
        Assert.False((bool?)planned["Security:Elevation:Enabled"]);
        Assert.Equal(9, (int?)planned["Security:MagicLink:RequestLimit"]);
    }

    [Fact]
    public void PlanChanges_AcceptsANewSensitiveValue()
    {
        var planned = Planned(Security, ("ConnectionStrings:Default", "\"Data Source=b.db\""));

        Assert.Equal("Data Source=b.db", (string?)planned["ConnectionStrings:Default"]);
    }

    [Theory]
    [InlineData("Security:Jwt:Issuer", "true")]
    [InlineData("Security:Jwt:Issuer", "5")]
    [InlineData("Security:Elevation:Enabled", "\"false\"")]
    [InlineData("Security:MagicLink:RequestLimit", "\"9\"")]
    [InlineData("Security:Jwt:Missing", "\"x\"")]
    [InlineData("Security:Jwt", "\"x\"")]
    [InlineData("ConnectionStrings:Default", "\"\"")]
    [InlineData("ConnectionStrings:Default", "\"  \"")]
    public void PlanChanges_RefusesWhatDoesNotFitTheExistingValue(string path, string json)
    {
        Assert.Equal(None, Root.PlanChanges(Security, Values((path, json))));
    }

    [Theory]
    [InlineData("Security:SecretKey")]
    [InlineData("Security:Jwt:SigningKey")]
    [InlineData("Security:MagicLink:ActualSecret")]
    public void PlanChanges_RefusesValuesThatHaveAGenerateOrRotateAction(string path)
    {
        Assert.Equal(None, Root.PlanChanges(Security, Values((path, "\"typed\""))));
    }

    [Fact]
    public void PlanChanges_ActionPathsAreEditableInOtherFiles()
    {
        var planned = Planned("other.json", ("Security:SecretKey", "\"typed\""));

        Assert.Equal("typed", (string?)planned["Security:SecretKey"]);
    }

    [Fact]
    public void PlanChanges_RefusesEverythingWhenOneChangeIsInvalid()
    {
        Assert.Equal(None, Root.PlanChanges(Security, Values(("Security:Jwt:Issuer", "\"you\""), ("Missing", "\"x\""))));
    }

    [Fact]
    public void PlanChanges_WithoutChanges_IsEmpty()
    {
        Assert.Empty(Planned(Security));
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("MagicLink")]
    [InlineData("TwoFactor")]
    [InlineData("Passkey")]
    [InlineData("EmailTwoFactor")]
    [InlineData("SubaccountSwitch")]
    public void MissingAccountModesCanBeDisplayedAndSaved(string mode)
    {
        var root = JsonNode.Parse("""{"Backend":{"Accounts":{"SignIn":true}}}""")!.AsObject();
        var path = $"Backend:Accounts:{mode}";
        var field = Assert.Single(AccountModeSettings.Missing(root, "backend.json"), field => field.Path == path);

        Assert.Equal("boolean", field.Type);
        Assert.Equal("true", field.Value);
        Assert.True(field.Editable);
        var changes = root.PlanChanges("backend.json", Values((path, "false")));
        Assert.False((bool?)changes.Match(() => throw new InvalidOperationException(), found => found[path]));
        Assert.Equal(None, root.PlanChanges("backend.json", Values((path, "\"false\""))));
        Assert.Equal(None, root.PlanChanges("other.json", Values((path, "false"))));
    }

    [Fact]
    public void MissingAccountModesDoNotReplaceAnIncompatibleSection()
    {
        var root = JsonNode.Parse("""{"Backend":{"Accounts":"invalid"}}""")!.AsObject();

        Assert.Empty(AccountModeSettings.Missing(root, "backend.json"));
        Assert.Equal(None, root.PlanChanges("backend.json", Values(("Backend:Accounts:Password", "false"))));
    }

    [Fact]
    public void MissingKnownValuesAreTypedAndUnknownOrConflictingPathsAreRejected()
    {
        var root = JsonNode.Parse("""{"Backend":{"Scheduler":{},"Accounts":"invalid"}}""")!.AsObject();
        var fields = KnownSettings.Fields(root, "backend.json", []);
        Assert.Equal(SchedulerConfiguration.DefaultTickSeconds,
            fields.Single(field => field.Path == "Backend:Scheduler:TickSeconds").Value.GetValue<int>());
        Assert.DoesNotContain(fields, field => field.Path.StartsWith("Backend:Accounts:"));
        Assert.NotEqual(None, root.PlanChanges("backend.json", Values(("Backend:Scheduler:TickSeconds", "30"))));
        Assert.Equal(None, root.PlanChanges("backend.json", Values(("Backend:Scheduler:TickSeconds", "\"30\""))));
        Assert.Equal(None, root.PlanChanges("backend.json", Values(("Backend:Scheduler:Unknown", "30"))));
        Assert.Equal(None, root.PlanChanges("backend.json", Values(("Backend:Accounts:SignIn", "false"))));
        Assert.Equal(None, root.PlanChanges("backend.json", Values(("Backend:Scheduler", "{}"))));
    }

    [Fact]
    public void ModuleSwitchesOnlyAcceptInstalledKeys()
    {
        var root = JsonNode.Parse("""{"Backend":{"Modules":{"forum":false,"uninstalled":true}}}""")!.AsObject();
        var fields = KnownSettings.Fields(root, "backend.json", ["forum", "sport"]);
        Assert.False(fields.Single(field => field.Path == "Backend:Modules:forum").Value.GetValue<bool>());
        Assert.True(fields.Single(field => field.Path == "Backend:Modules:sport").Value.GetValue<bool>());
        Assert.NotEqual(None, root.PlanChanges("backend.json", Values(("Backend:Modules:sport", "false")), ["forum", "sport"]));
        Assert.Equal(None, root.PlanChanges("backend.json", Values(("Backend:Modules:uninstalled", "false")), ["forum"]));
        Assert.Equal(None, root.PlanChanges("backend.json", Values(("Backend:Modules:core", "false")), ["forum"]));
        Assert.Equal(None, root.PlanChanges("backend.json", Values(("Backend:Modules:forum", "\"false\"")), ["forum"]));
    }

    [Fact]
    public void StructuredValuesPreserveMaskedConnectionsAndRejectOverlappingChanges()
    {
        var root = JsonNode.Parse("""{"ConnectionStrings":{"Default":"a-secret-value","Old":"to-remove"}}""")!.AsObject();
        var values = Values(("ConnectionStrings", """{"Default":"a-..........ue","New":"new-secret"}"""));
        var planned = root.PlanChanges("database.json", values).Match(
            () => throw new InvalidOperationException(), found => found);
        Assert.Equal("a-secret-value", (string?)planned["ConnectionStrings"]!["Default"]);
        Assert.Null(planned["ConnectionStrings"]!["Old"]);
        Assert.Equal("new-secret", (string?)planned["ConnectionStrings"]!["New"]);
        Assert.Equal(None, root.PlanChanges("database.json", Values(
            ("ConnectionStrings", "{}"), ("ConnectionStrings:Default", "\"new\""))));
        Assert.Equal(None, root.PlanChanges("database.json", Values(("ConnectionStrings", """{"Bad":1}"""))));
    }

    [Fact]
    public void ExistingArrayEntriesRemainEditableAndStoredCasingIsPreserved()
    {
        var root = JsonNode.Parse("""{"custom":{"items":[{"name":"old"}]}}""")!.AsObject();
        var planned = root.PlanChanges("other.json", Values(("Custom:Items:0:Name", "\"new\"")))
            .GetOrElse(new Dictionary<string, JsonNode?>());
        Assert.Equal("new", (string?)planned["custom:items:0:name"]);
    }

    [Fact]
    public void ConnectionMapsPreserveBlankSecretsAllowExplicitClearingAndRejectDuplicateNames()
    {
        var root = JsonNode.Parse("""{"ConnectionStrings":{"Default":"secret","Empty":""}}""")!.AsObject();
        var planned = root.PlanChanges("database.json", Values(("ConnectionStrings",
            """{"Default":"","Empty":"","New":"new-secret"}"""))).GetOrElse(new Dictionary<string, JsonNode?>());
        Assert.Equal("secret", (string?)planned["ConnectionStrings"]!["Default"]);
        Assert.Equal("", (string?)planned["ConnectionStrings"]!["Empty"]);
        Assert.Equal("new-secret", (string?)planned["ConnectionStrings"]!["New"]);
        Assert.NotEqual(None, root.PlanChanges("database.json", Values(("ConnectionStrings", """{"Default":null}"""))));
        Assert.Equal(None, root.PlanChanges("database.json", Values(("ConnectionStrings",
            """{"Default":"x","default":"y"}"""))));
        Assert.Equal(None, root.PlanChanges("database.json", Values(
            ("ConnectionStrings:Default", "\"x\""), ("connectionstrings:default", "\"y\""))));
    }

    [Fact]
    public void ConnectionMapRenamesPreserveUniqueMaskedValuesAndRejectAmbiguousMasks()
    {
        var root = JsonNode.Parse(
            """{"ConnectionStrings":{"Default":"a-secret-value","One":"cd1234xy","Two":"ef5678xy"}}""")!.AsObject();
        var renamed = root.PlanChanges("database.json", Values(("ConnectionStrings",
            """{"Renamed":"a-..........ue","one":"cd....xy","Two":"ef....xy"}""")))
            .GetOrElse(new Dictionary<string, JsonNode?>());

        Assert.Equal("a-secret-value", (string?)renamed["ConnectionStrings"]!["Renamed"]);
        Assert.Equal("cd1234xy", (string?)renamed["ConnectionStrings"]!["one"]);
        Assert.Equal("ef5678xy", (string?)renamed["ConnectionStrings"]!["Two"]);
        Assert.Equal(None, root.PlanChanges("database.json", Values(("ConnectionStrings", """{"Renamed":"ab....xy"}"""))));
        Assert.NotEqual(None, root.PlanChanges("database.json", Values(("ConnectionStrings", """{"New":"new-secret"}"""))));
    }

    [Fact]
    public void ConnectionMapRejectsAStaleMaskedValueButAcceptsANewSecret()
    {
        var root = JsonNode.Parse(
            """{"ConnectionStrings":{"Default":"a-current-secret"}}""")!.AsObject();
        Assert.Equal(None, root.PlanChanges("database.json", Values(
            ("ConnectionStrings", """{"Default":"a...............et"}"""))));
        var planned = root.PlanChanges("database.json", Values(
            ("ConnectionStrings", """{"Default":"a-new-secret"}"""))).Match(
                () => throw new InvalidOperationException(), found => found);
        Assert.Equal("a-new-secret", (string?)planned["ConnectionStrings"]!["Default"]);
    }

    [Fact]
    public void MissingDefaultsRespectPropertyCasingAndModuleSchemaDefaults()
    {
        var root = JsonNode.Parse("""{"backend":{"accounts":{},"scheduler":{}}}""")!.AsObject();
        var fields = KnownSettings.Fields(root, "backend.json", []);
        Assert.True(fields.Single(field => field.Path == "backend:accounts:SignIn").Value.GetValue<bool>());
        Assert.Equal(SchedulerConfiguration.DefaultTickSeconds,
            fields.Single(field => field.Path == "backend:scheduler:TickSeconds").Value.GetValue<int>());
        Assert.Equal("Post", KnownSettings.Fields(new JsonObject(), "forum.json", [])
            .Single(field => field.Path == "Forum:Moderation").Value.GetValue<string>());
        Assert.Equal("README.md", KnownSettings.Fields(new JsonObject(), "markdownviewer.json", [])
            .Single(field => field.Path == "MarkdownViewer:DefaultDocument").Value.GetValue<string>());
    }

    [Fact]
    public void MissingStringListAcceptsArrayButNotOtherTypes()
    {
        var root = new JsonObject();
        var fields = KnownSettings.Fields(root, "frontend.json", []);
        Assert.IsType<JsonArray>(fields.Single(field => field.Path == "Frontend:StaticFiles:CompressedMimeTypes").Value);
        Assert.NotEqual(None, root.PlanChanges("frontend.json", Values(("Frontend:StaticFiles:CompressedMimeTypes", """["text/plain"]"""))));
        Assert.Equal(None, root.PlanChanges("frontend.json", Values(("Frontend:StaticFiles:CompressedMimeTypes", "\"text/plain\""))));
    }
}
