using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IMagicLinkCommands
{
    Task Issue(Guid userId, MagicLink magicLink, CancellationToken cancellationToken = default);
    Task<bool> TryIssue(Guid userId, MagicLink magicLink, CancellationToken cancellationToken = default);
    Task<bool> LockForFunction(TokenNonce nonce, Guid userId, MagicLinkFunction function, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> ConsumeForFunction(TokenNonce nonce, Guid userId, MagicLinkFunction function, DateTimeOffset consumedAt, CancellationToken cancellationToken = default);
    Task<Option<MagicLinkRedemption>> Consume(TokenNonce nonce, DateTimeOffset consumedAt, CancellationToken cancellationToken = default);
    Task<Option<Guid>> ConsumePasswordReset(TokenNonce nonce, DateTimeOffset consumedAt, CancellationToken cancellationToken = default);
    Task<int> RevokeForUser(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);
    Task<int> DeleteExpired(DateTimeOffset now, CancellationToken cancellationToken = default);
}
