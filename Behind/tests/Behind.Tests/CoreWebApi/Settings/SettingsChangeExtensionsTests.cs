using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;
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
}
