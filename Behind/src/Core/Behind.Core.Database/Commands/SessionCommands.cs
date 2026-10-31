using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class SessionCommands(AbstractCoreContext context) : ISessionCommands
{
    public async Task<Guid> CreateForUserId(
        Guid userId,
        ValidSession session,
        CancellationToken cancellationToken = default)
    {
        await context.GetRepository<Session, Guid>().AddAsync(session, cancellationToken);

        var entry = context.Entry(session);
        entry.Property<Guid>("UserId").CurrentValue = userId;
        await context.SaveChangesAsync(cancellationToken);

        return entry.Property<Guid>("Id").CurrentValue;
    }

    public async Task<bool> RenewAccessToken(
        Guid sessionId,
        TokenNonce expectedNonce,
        AccessToken accessToken,
        CancellationToken cancellationToken = default)
    {
        var session = await context.Sessions
            .Where(s => EF.Property<Guid>(s, "Id") == sessionId
                        && EF.Property<Guid?>(s, "InvalidatedById") == null
                        && EF.Property<DateTimeOffset?>(s, "InvalidatedAt") == null
                        && s.AccessToken.Nonce == expectedNonce)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return false;
        }

        var token = context.Entry(session).ComplexProperty(s => s.AccessToken);
        token.Property(t => t.Nonce).CurrentValue = accessToken.Nonce;
        token.Property(t => t.IssuedAt).CurrentValue = accessToken.IssuedAt;
        token.Property(t => t.ExpiresAt).CurrentValue = accessToken.ExpiresAt;

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
