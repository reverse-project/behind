using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IMagicLinkCommands
{
    Task Issue(Guid userId, MagicLink magicLink, CancellationToken cancellationToken = default);
    Task<Option<Guid>> Consume(TokenNonce nonce, DateTimeOffset consumedAt, CancellationToken cancellationToken = default);
    Task<int> DeleteExpired(DateTimeOffset now, CancellationToken cancellationToken = default);
}
