using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISessionCommands
{
    Task<Guid> CreateForUserId(Guid userId, ValidSession session, CancellationToken cancellationToken = default);
    Task<Guid> CreateImpersonation(Guid impersonatorId, Guid userId, ValidSession session, CancellationToken cancellationToken = default);
    Task<bool> RenewAccessToken(Guid sessionId, TokenNonce expectedNonce, AccessToken accessToken, CancellationToken cancellationToken = default);
    Task InvalidateSession(Guid sessionId, Guid invalidatedBy, DateTimeOffset invalidatedAt, CancellationToken cancellationToken = default);
    Task InvalidateAllSessions(Guid userId, Guid invalidatedBy, DateTimeOffset invalidatedAt, CancellationToken cancellationToken = default);
}
