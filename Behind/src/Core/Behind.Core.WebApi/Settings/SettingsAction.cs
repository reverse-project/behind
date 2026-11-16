using Behind.Common;
using Behind.Core.Common.Configuration;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public enum SettingsActionKind
{
    Generate,
    Rotate
}

public sealed record GeneratedSecret(string Value, Option<string> OtpUri);

public sealed record SettingsAction(
    string Path,
    SettingsActionKind Kind,
    bool CreatesMissing = false,
    Func<GeneratedSecret>? Generate = null)
{
    public const string SecurityFile = "security.json";
    public const string AdministratorAccount = "admin";
    public const string DeveloperAccount = "developer";

    public static readonly SettingsAction AdministratorSecretKey = new(
        "Security:SecretKey",
        SettingsActionKind.Generate,
        true,
        () => GenerateTotp(AdministratorAccount));

    public static readonly SettingsAction DeveloperSecretKey = new(
        "Security:Elevation:DeveloperSecretKey",
        SettingsActionKind.Generate,
        true,
        () => GenerateTotp(DeveloperAccount));

    public static readonly SettingsAction JwtSigningKey = new(
        "Security:Jwt:SigningKey",
        SettingsActionKind.Generate,
        Generate: () => new GeneratedSecret(JwtSettings.GenerateSigningKey(), None));

    public static readonly SettingsAction MagicLinkSecret = new(
        "Security:MagicLink:ActualSecret",
        SettingsActionKind.Rotate);

    public static readonly IReadOnlyList<SettingsAction> All =
        [AdministratorSecretKey, DeveloperSecretKey, JwtSigningKey, MagicLinkSecret];

    public string Name => Kind.ToString().ToLowerInvariant();

    private static GeneratedSecret GenerateTotp(string account)
    {
        var totp = Totp.Generate();

        return new GeneratedSecret(totp.ToConfigurationValue(), Some(totp.ToOtpUri(account, Totp.Issuer)));
    }
}
