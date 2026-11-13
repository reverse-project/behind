namespace Behind.Core.Common.Configuration;

public sealed record AccountsConfiguration
{
    public const string SectionName = $"{BackendConfiguration.SectionName}:Accounts";

    public bool SignIn { get; init; } = true;

    public bool SignUp { get; init; } = true;

    public bool Password { get; init; } = true;

    public bool MagicLink { get; init; } = true;

    public bool TwoFactor { get; init; } = true;

    public bool Passkey { get; init; } = true;

    public bool EmailTwoFactor { get; init; } = true;
}
