using Behind.Common;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class SessionCommands(AbstractCoreContext context) : ISessionCommands
{
    public Task<Guid> CreateForUserId(
        Guid userId,
        ValidSession session,
        CancellationToken cancellationToken = default) =>
        Create(userId, None, session, cancellationToken);

    public Task<Guid> CreateImpersonation(
        Guid impersonatorId,
        Guid userId,
        ValidSession session,
        CancellationToken cancellationToken = default) =>
        Create(userId, Some(impersonatorId), session, cancellationToken);

    private async Task<Guid> Create(
        Guid userId,
        Option<Guid> impersonatorId,
        ValidSession session,
        CancellationToken cancellationToken)
    {
        await context.GetRepository<Session, Guid>().AddAsync(session, cancellationToken);

        var entry = context.Entry(session);
        entry.Property<Guid>("UserId").CurrentValue = userId;
        entry.Property<Guid?>("ImpersonatorId").CurrentValue = impersonatorId.Match(() => (Guid?)null, id => id);
        await context.SaveChangesAsync(cancellationToken);

        return entry.Property<Guid>("Id").CurrentValue;
    }

    public async Task<bool> RenewTokens(
        Guid sessionId,
        TokenNonce expectedAccessNonce,
        TokenNonce expectedRefreshNonce,
        AccessToken accessToken,
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default)
    {
        var session = await context.Sessions
            .Where(s => EF.Property<Guid>(s, "Id") == sessionId
                        && EF.Property<Guid?>(s, "InvalidatedById") == null
                        && EF.Property<DateTimeOffset?>(s, "InvalidatedAt") == null
                        && s.AccessToken.Nonce == expectedAccessNonce
                        && s.RefreshToken.Nonce == expectedRefreshNonce)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return false;
        }

        var entry = context.Entry(session);

        var access = entry.ComplexProperty(s => s.AccessToken);
        access.Property(t => t.Nonce).CurrentValue = accessToken.Nonce;
        access.Property(t => t.IssuedAt).CurrentValue = accessToken.IssuedAt;
        access.Property(t => t.ExpiresAt).CurrentValue = accessToken.ExpiresAt;

        var refresh = entry.ComplexProperty(s => s.RefreshToken);
        refresh.Property(t => t.Nonce).CurrentValue = refreshToken.Nonce;
        refresh.Property(t => t.IssuedAt).CurrentValue = refreshToken.IssuedAt;
        refresh.Property(t => t.ExpiresAt).CurrentValue = refreshToken.ExpiresAt;

        return await context.SaveChangesAsync(cancellationToken) > 0;
    }

    public Task InvalidateSession(
        Guid sessionId,
        Guid invalidatedBy,
        DateTimeOffset invalidatedAt,
        CancellationToken cancellationToken = default) =>
        InvalidateWhere(
            context.Sessions.Where(s => EF.Property<Guid>(s, "Id") == sessionId),
            invalidatedBy,
            invalidatedAt,
            cancellationToken);

    public Task InvalidateAllSessions(
        Guid userId,
        Guid invalidatedBy,
        DateTimeOffset invalidatedAt,
        CancellationToken cancellationToken = default) =>
        InvalidateWhere(
            context.Sessions.Where(s => EF.Property<Guid>(s, "UserId") == userId),
            invalidatedBy,
            invalidatedAt,
            cancellationToken);

    private async Task InvalidateWhere(
        IQueryable<ValidSession> sessions,
        Guid invalidatedBy,
        DateTimeOffset invalidatedAt,
        CancellationToken cancellationToken)
    {
        var active = await sessions
            .Where(s => EF.Property<Guid?>(s, "InvalidatedById") == null
                        && EF.Property<DateTimeOffset?>(s, "InvalidatedAt") == null)
            .ToListAsync(cancellationToken);

        foreach (var session in active)
        {
            var entry = context.Entry(session);
            entry.Property<Guid?>("InvalidatedById").CurrentValue = invalidatedBy;
            entry.Property<DateTimeOffset?>("InvalidatedAt").CurrentValue = invalidatedAt;
        }

        if (active.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
