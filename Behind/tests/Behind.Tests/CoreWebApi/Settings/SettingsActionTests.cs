using Behind.Core.WebApi.Settings;

namespace Behind.Tests.CoreWebApi.Settings;

public sealed class SettingsActionTests
{
    [Theory]
    [InlineData("Security:SecretKey", SettingsActionKind.Generate)]
    [InlineData("Security:Elevation:DeveloperSecretKey", SettingsActionKind.Generate)]
    [InlineData("Security:Jwt:SigningKey", SettingsActionKind.Generate)]
    [InlineData("Security:MagicLink:ActualSecret", SettingsActionKind.Rotate)]
    public void FindAction_KnowsTheSecretsOfTheSecurityFile(string path, SettingsActionKind kind)
    {
        var action = Assert.IsType<SettingsAction>(
            "security.json".FindAction(path).Match<object>(() => string.Empty, found => found));

        Assert.Equal(kind, action.Kind);
        Assert.Equal(kind.ToString().ToLowerInvariant(), action.Name);
    }

    [Fact]
    public void FindAction_IgnoresOtherFilesAndPaths()
    {
        Assert.Equal(None, "backend.json".FindAction("Security:SecretKey"));
        Assert.Equal(None, "security.json".FindAction("Security:Jwt:Issuer"));
    }

    [Fact]
    public void FindAction_WithKind_RequiresTheKindToMatch()
    {
        Assert.NotEqual(None, "security.json".FindAction("Security:Jwt:SigningKey", SettingsActionKind.Generate));
        Assert.Equal(None, "security.json".FindAction("Security:Jwt:SigningKey", SettingsActionKind.Rotate));
        Assert.Equal(None, "security.json".FindAction("Security:MagicLink:ActualSecret", SettingsActionKind.Generate));
    }

    [Fact]
    public void TotpActions_GenerateAnAuthenticatorUriForTheirAccount()
    {
        var administrator = SettingsAction.AdministratorSecretKey.Generate!();
        var developer = SettingsAction.DeveloperSecretKey.Generate!();

        Assert.StartsWith("totp://", administrator.Value);
        Assert.Contains(":admin?", administrator.OtpUri.Match(() => string.Empty, uri => uri));
        Assert.Contains(":developer?", developer.OtpUri.Match(() => string.Empty, uri => uri));
    }

    [Fact]
    public void JwtAction_GeneratesAKeyWithoutAnAuthenticatorUri()
    {
        var generated = SettingsAction.JwtSigningKey.Generate!();

        Assert.Equal(64, generated.Value.Length);
        Assert.Equal(None, generated.OtpUri);
    }

    [Fact]
    public void OnlyTheElevationKeysAreCreatedWhenMissing()
    {
        Assert.Equal(
            ["Security:SecretKey", "Security:Elevation:DeveloperSecretKey"],
            SettingsAction.All.Where(action => action.CreatesMissing).Select(action => action.Path));
    }
}
