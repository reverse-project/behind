using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class SessionQueries(AbstractCoreContext context, IOptions<SecurityConfiguration>? security = null) : ISessionQueries
{
    public async Task<Option<Session>> TryFindById(Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await TryFindActor(id, cancellationToken);
        var permitted = await actor.Match(
            () => Task.FromResult(false),
            found => found.ImpersonatorId.Match(
                () => Task.FromResult(true),
                async impersonatorId => await context.Users.AnyAsync(
                    user => EF.Property<Guid>(user, "Id") == impersonatorId, cancellationToken)
                    && await new WallOfShameQueries(context).FindActiveBan(
                        impersonatorId, DateTimeOffset.UtcNow, cancellationToken) == None));
        return permitted
            ? await context.GetRepository<Session, Guid>().TryFindAsync(id, cancellationToken)
            : None;
    }

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

    public async Task<Option<ISessionQueries.SessionActor>> TryFindActor(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await context.Sessions
            .Where(s => EF.Property<Guid>(s, "Id") == id)
            .Select(s => new
            {
                UserId = EF.Property<Guid>(s, "UserId"),
                ImpersonatorId = EF.Property<Guid?>(s, "ImpersonatorId"),
                FamilyPrimaryUserId = EF.Property<Guid?>(s, "FamilyPrimaryId"),
                FamilyManagerUserId = EF.Property<Guid?>(s, "FamilyManagerId")
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var isRestrictedAdult = false;
        if (row?.FamilyPrimaryUserId is { } primaryId)
        {
            if (security?.Value.Accounts.Subaccounts == false)
            {
                return None;
            }
            var fromLinkedProfile = await SubaccountSwitchScope.IsLinked(context, primaryId, cancellationToken);
            isRestrictedAdult = fromLinkedProfile && row.UserId != primaryId;
            var scope = new SubaccountSwitchScope(primaryId, fromLinkedProfile);
            var managerValid = row.FamilyManagerUserId is not { } managerId
                || fromLinkedProfile
                    && await scope.Profiles(context).AnyAsync(user => EF.Property<Guid>(user, "Id") == managerId, cancellationToken)
                    && await context.ActiveSubaccounts().AnyAsync(link => EF.Property<Guid>(link, "PrimaryUserId") == managerId
                        && EF.Property<Guid>(link, "UserId") == primaryId, cancellationToken)
                    && await new WallOfShameQueries(context).FindActiveBan(managerId, DateTimeOffset.UtcNow, cancellationToken) == None;
            var actorScope = row.FamilyManagerUserId is { } manager
                ? new SubaccountSwitchScope(manager, false) : scope;
            var linked = managerValid
                && await actorScope.Profiles(context).AnyAsync(user => EF.Property<Guid>(user, "Id") == row.UserId, cancellationToken)
                && (row.UserId != primaryId || fromLinkedProfile
                    || await context.ActiveSubaccounts().AnyAsync(link => EF.Property<Guid>(link, "PrimaryUserId") == primaryId
                        && context.Users.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active)
                            .Any(user => EF.Property<Guid>(user, "Id") == EF.Property<Guid>(link, "UserId")), cancellationToken))
                && (row.UserId == primaryId
                    || await scope.Profiles(context).AnyAsync(user => EF.Property<Guid>(user, "Id") == primaryId, cancellationToken));
            if (!linked || await new WallOfShameQueries(context).FindActiveBan(primaryId, DateTimeOffset.UtcNow, cancellationToken) != None)
            {
                return None;
            }
        }

        return row is null
            ? None
            : Some(new ISessionQueries.SessionActor(
                row.UserId,
                row.ImpersonatorId.HasValue ? Some(row.ImpersonatorId.Value) : None,
                isRestrictedAdult));
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
