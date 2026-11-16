using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IMagicLinkQueries
{
    Task<bool> IsUsableForFunction(TokenNonce nonce, Guid userId, MagicLinkFunction function, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> HasIssuedSince(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default);
    Task<Option<Guid>> FindPasswordResetUser(TokenNonce nonce, DateTimeOffset now, CancellationToken cancellationToken = default);
}
