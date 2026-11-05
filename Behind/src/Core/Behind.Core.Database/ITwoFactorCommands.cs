using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ITwoFactorCommands
{
    Task SaveTotp(Guid userId, string secret, CancellationToken cancellationToken = default);

    Task<bool> ConfirmTotp(Guid userId, long step, CancellationToken cancellationToken = default);

    Task<bool> AdvanceTotpStep(Guid userId, long step, CancellationToken cancellationToken = default);

    Task<bool> RemoveTotp(Guid userId, CancellationToken cancellationToken = default);

    Task AddPasskey(Guid userId, UserPasskey passkey, CancellationToken cancellationToken = default);

    Task<bool> UpdatePasskeyCounter(
        Guid userId,
        Guid passkeyId,
        long signCount,
        CancellationToken cancellationToken = default);

    Task<bool> RemovePasskey(Guid userId, Guid passkeyId, CancellationToken cancellationToken = default);

    Task ReplaceRecoveryCodes(
        Guid userId,
        IReadOnlyList<string> hashes,
        CancellationToken cancellationToken = default);

    Task<bool> ConsumeRecoveryCode(Guid userId, Guid codeId, CancellationToken cancellationToken = default);

    Task RemoveRecoveryCodes(Guid userId, CancellationToken cancellationToken = default);
}
