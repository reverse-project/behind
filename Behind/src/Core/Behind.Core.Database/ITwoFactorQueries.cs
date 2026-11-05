using Behind.Common;

namespace Behind.Core.Database;

public interface ITwoFactorQueries
{
    public sealed record TotpInfo(string Secret, bool IsConfirmed, long LastUsedStep);

    public sealed record PasskeyInfo(
        Guid Id,
        string Name,
        string CredentialId,
        string PublicKey,
        long SignCount,
        Option<DateTimeOffset> CreatedAt);

    public sealed record RecoveryCodeInfo(Guid Id, string Hash);

    public sealed record TwoFactorStatus(
        bool TotpEnabled,
        IReadOnlyList<PasskeyInfo> Passkeys,
        int RecoveryCodesRemaining)
    {
        public bool IsEnabled => TotpEnabled || Passkeys.Count > 0;
    }

    Task<TwoFactorStatus> GetStatus(Guid userId, CancellationToken cancellationToken = default);

    Task<Option<TotpInfo>> FindTotp(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PasskeyInfo>> GetPasskeys(Guid userId, CancellationToken cancellationToken = default);

    Task<Option<PasskeyInfo>> FindPasskey(
        Guid userId,
        string credentialId,
        CancellationToken cancellationToken = default);

    Task<bool> IsCredentialRegistered(string credentialId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecoveryCodeInfo>> GetRecoveryCodes(
        Guid userId,
        CancellationToken cancellationToken = default);
}
