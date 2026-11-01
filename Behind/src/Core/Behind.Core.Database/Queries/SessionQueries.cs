using System.Linq.Expressions;
using Behind.Common;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class SessionQueries(AbstractCoreContext context) : ISessionQueries
{
    public Task<Option<Session>> TryFindById(Guid id, CancellationToken cancellationToken = default) =>
        context.GetRepository<Session, Guid>().TryFindAsync(id, cancellationToken);

    public async Task<Option<(Session Session, Guid UserId)>> TryFindByIdWithUserId(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        var userId = await context.Sessions
            .Where(s => EF.Property<Guid>(s, "Id") == id)
            .Select(s => (Guid?)EF.Property<Guid>(s, "UserId"))
            .FirstOrDefaultAsync(cancellationToken);

        if (userId is null)
        {
            return None;
        }

        var session = await TryFindById(id, cancellationToken);
        return session.Map(s => (s, userId.Value));
    }

    public async Task<ISessionQueries.UserSessions> GetUserSessions(
        Guid userId,
        DateTimeOffset now,
        Flipper active,
        Flipper ended,
        CancellationToken cancellationToken = default)
    {
        var owned = context.Sessions.Where(s => EF.Property<Guid>(s, "UserId") == userId);

        return new ISessionQueries.UserSessions(
            await PageOf(owned.Where(IsActive(now)), active, cancellationToken),
            await PageOf(owned.Where(IsEnded(now)), ended, cancellationToken));
    }

    private async Task<PagedResult<ISessionQueries.SessionSummary>> PageOf(
        IQueryable<ValidSession> sessions,
        Flipper flipper,
        CancellationToken cancellationToken)
    {
        var total = await sessions.LongCountAsync(cancellationToken);
        var rows = await flipper.Apply(Summaries(sessions)).ToListAsync(cancellationToken);

        return new PagedResult<ISessionQueries.SessionSummary>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0 ? Some<IEnumerable<ISessionQueries.SessionSummary>>(rows.Select(ToSummary).ToList()) : None);
    }

    private static Expression<Func<ValidSession, bool>> IsActive(DateTimeOffset now) =>
        s => EF.Property<Guid?>(s, "InvalidatedById") == null
             && EF.Property<DateTimeOffset?>(s, "InvalidatedAt") == null
             && s.RefreshToken.ExpiresAt > now;

    private static Expression<Func<ValidSession, bool>> IsEnded(DateTimeOffset now) =>
        s => EF.Property<Guid?>(s, "InvalidatedById") != null
             || EF.Property<DateTimeOffset?>(s, "InvalidatedAt") != null
             || s.RefreshToken.ExpiresAt <= now;

    private IQueryable<SessionRow> Summaries(IQueryable<ValidSession> sessions) =>
        sessions
            .OrderByDescending(s => s.RefreshToken.IssuedAt)
            .ThenByDescending(s => EF.Property<Guid>(s, "Id"))
            .Select(s => new SessionRow(
                EF.Property<Guid>(s, "Id"),
                s.RefreshToken.IssuedAt,
                s.AccessToken.IssuedAt,
                s.RefreshToken.ExpiresAt,
                s.RemoteAddress,
                context.UserAgents
                    .Where(a => EF.Property<Guid>(a, "Id") == EF.Property<Guid>(s, "UserAgentId"))
                    .Select(a => a.Value)
                    .FirstOrDefault(),
                EF.Property<Guid?>(s, "InvalidatedById"),
                EF.Property<DateTimeOffset?>(s, "InvalidatedAt")))
            .AsNoTracking();

    private static ISessionQueries.SessionSummary ToSummary(SessionRow row) =>
        new(
            row.Id,
            row.StartedAt,
            row.LastActiveAt,
            row.ExpiresAt,
            row.RemoteAddress,
            row.UserAgent ?? string.Empty,
            row.InvalidatedById.HasValue || row.InvalidatedAt.HasValue,
            row.InvalidatedAt.HasValue ? Some(row.InvalidatedAt.Value) : None);

    private sealed record SessionRow(
        Guid Id,
        DateTimeOffset StartedAt,
        DateTimeOffset LastActiveAt,
        DateTimeOffset ExpiresAt,
        RemoteAddress RemoteAddress,
        string? UserAgent,
        Guid? InvalidatedById,
        DateTimeOffset? InvalidatedAt);
}
