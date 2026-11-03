namespace Behind.Core.Common.Configuration;

public sealed record AccountsConfiguration
{
    public const string SectionName = $"{BackendConfiguration.SectionName}:Accounts";

    public bool SignIn { get; init; } = true;

    public bool SignUp { get; init; } = true;
}
