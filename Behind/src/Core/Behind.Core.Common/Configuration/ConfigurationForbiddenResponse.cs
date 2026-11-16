namespace Behind.Core.Common.Configuration;

public sealed record ConfigurationForbiddenResponse
{
    public string Code { get; }
    public string Message { get; }

    private ConfigurationForbiddenResponse(string code, string message)
    {
        Code = code;
        Message = message;
    }

    public static readonly ConfigurationForbiddenResponse InvalidTotp = new(
        "invalid_totp",
        "The provided TOTP code is invalid.");

    public static readonly ConfigurationForbiddenResponse ConfigurationReadOnly = new(
        "configuration_read_only",
        "Runtime configuration changes are disabled. Update configuration through the configured external provider.");
}