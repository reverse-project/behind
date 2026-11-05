namespace Behind.Core.Domain;

public sealed record UserPasskey(string Name, string CredentialId, string PublicKey, long SignCount = 0)
{
    public const int MaxNameLength = 100;
    public const int MaxCredentialIdLength = 1_400;
    public const int MaxPublicKeyLength = 2_000;
}
