using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public static class SubaccountLinkEndpoints
{
    public sealed record Candidate(Guid UserId, string DisplayName);
    public sealed record Invitation(Guid RequestId, Guid UserId, string DisplayName);

    private static IQueryable<HumanUser> ActiveUsers(AbstractCoreContext db) =>
        db.Users.InDeletionState(db, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

    private static IQueryable<HumanUser> Roots(AbstractCoreContext db) =>
        ActiveUsers(db).Where(user => !db.ActiveSubaccounts().Any(link => EF.Property<Guid>(link, "UserId") == EF.Property<Guid>(user, "Id")));

    private static IQueryable<HumanUser> EligibleChildren(AbstractCoreContext db, Guid parentId) =>
        from user in ActiveUsers(db)
        let userId = EF.Property<Guid>(user, "Id")
        where userId != parentId && user.Emails.Any(email => email.Value.Trim() != "")
            && !db.ActiveSubaccounts().Any(link => EF.Property<Guid>(link, "PrimaryUserId") == userId)
            && !db.ActiveSubaccounts().Any(link => EF.Property<Guid>(link, "UserId") == userId
                && (EF.Property<Guid>(link, "PrimaryUserId") == parentId || !link.IsChild))
        select user;

    private static async Task<HumanUser?> Actor(HttpContext http, ISessionQueries sessions, AbstractCoreContext db,
        bool allowSwitchedPrimary = false, bool allowLinkedRecipient = false)
    {
        http.Response.Headers.CacheControl = "no-store";
        var actor = (await http.FindCurrentSession(sessions)).Match<CurrentSession?>(() => null, value => value);
        if (actor is null || actor.IsImpersonated
            || actor.FamilyPrimaryUserId.Match(() => false, familyId => !allowSwitchedPrimary || familyId != actor.UserId))
        {
            return null;
        }
        return await (allowLinkedRecipient ? ActiveUsers(db) : Roots(db))
            .SingleOrDefaultAsync(user => EF.Property<Guid>(user, "Id") == actor.UserId, http.RequestAborted);
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> Candidates(string? query, HttpContext http, ISessionQueries sessions, AbstractCoreContext db,
        int? page = null, int? pageSize = null)
    {
        var managers = await SubaccountEndpoints.GetFamilyManager(http, sessions, db, allowSwitchedPrimary: true);
        foreach (var manager in managers.AsEnumerable())
        {
            if (!await Roots(db).AnyAsync(user => EF.Property<Guid>(user, "Id") == manager.PrimaryUserId, http.RequestAborted))
            {
                return Results.Forbid();
            }
            var parentId = manager.PrimaryUserId;
            var search = query?.Trim().ToLowerInvariant() ?? "";
            var pagination = Flipper.Create(page, pageSize, ManagedListExtensions.DefaultPageSize, ManagedListExtensions.MaxPageSize);
            var candidates = EligibleChildren(db, parentId).Where(user => user.PendingLinkRequestId == null && user.DisplayName.ToLower().Contains(search))
                .OrderBy(user => user.DisplayName).ThenBy(user => EF.Property<Guid>(user, "Id"));
            var rows = await pagination.Apply(candidates).Select(user => new Candidate(EF.Property<Guid>(user, "Id"), user.DisplayName)).ToListAsync(http.RequestAborted);
            return Results.Ok(rows.Select(row => row with { DisplayName = row.UserId.ToUserLabel(row.DisplayName) }));
        }
        return Results.Forbid();
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> RequestLink(Guid userId, HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var managers = await SubaccountEndpoints.GetFamilyManager(http, sessions, db, allowSwitchedPrimary: false);
        foreach (var manager in managers.AsEnumerable())
        {
            if (!await Roots(db).AnyAsync(user => EF.Property<Guid>(user, "Id") == manager.PrimaryUserId, http.RequestAborted))
            {
                return Results.Forbid();
            }
            var parentId = manager.PrimaryUserId;
            var child = await EligibleChildren(db, parentId).SingleOrDefaultAsync(user => EF.Property<Guid>(user, "Id") == userId,
                http.RequestAborted);
            if (child is null || child.PendingLinkRequestId.HasValue)
            {
                return Results.Conflict();
            }
            db.Entry(child).Property(user => user.PendingParentId).CurrentValue = parentId;
            db.Entry(child).Property(user => user.PendingLinkRequestId).CurrentValue = Guid.NewGuid();
            await db.SaveChangesAsync(http.RequestAborted);
            return Results.NoContent();
        }
        return Results.Forbid();
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> Requests(HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var child = await Actor(http, sessions, db, allowLinkedRecipient: true);
        if (child is null)
        {
            return Results.Forbid();
        }
        List<Invitation> result = [];
        if (child.PendingParentId is { } parentId && child.PendingLinkRequestId is { } requestId)
        {
            var parent = await Roots(db).SingleOrDefaultAsync(user => EF.Property<Guid>(user, "Id") == parentId, http.RequestAborted);
            result.Add(new(requestId, parentId, parentId.ToUserLabel(parent?.DisplayName)));
        }
        return Results.Ok(result);
    }

    [Authorize, IgnoreETag]
    public static Task<IResult> Confirm(Guid requestId, HttpContext http, ISessionQueries sessions, AbstractCoreContext db) =>
        Resolve(requestId, true, http, sessions, db);

    [Authorize, IgnoreETag]
    public static Task<IResult> Decline(Guid requestId, HttpContext http, ISessionQueries sessions, AbstractCoreContext db) =>
        Resolve(requestId, false, http, sessions, db);

    private static async Task<IResult> Resolve(Guid requestId, bool accept, HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var child = await Actor(http, sessions, db, allowLinkedRecipient: true);
        if (child is null)
        {
            return Results.Forbid();
        }
        if (child.PendingLinkRequestId != requestId || child.PendingParentId is not { } parentId)
        {
            return Results.NotFound();
        }
        if (accept)
        {
            var childId = db.GetKey(child);
            var parent = await Roots(db).SingleOrDefaultAsync(user => EF.Property<Guid>(user, "Id") == parentId, http.RequestAborted);
            if (parent is null ||
                !await EligibleChildren(db, parentId).AnyAsync(user => EF.Property<Guid>(user, "Id") == childId, http.RequestAborted))
            {
                return Results.Conflict();
            }
            var previous = await db.LatestSubaccounts().SingleOrDefaultAsync(link =>
                EF.Property<Guid>(link, "PrimaryUserId") == parentId
                && EF.Property<Guid>(link, "UserId") == childId, http.RequestAborted);
            if (previous is null)
            {
                db.Subaccounts.Add(new Subaccount(parent, child, true));
            }
            else if (!previous.IsActive)
            {
                db.AppendSubaccountVersion(previous, isChild: true, isActive: true);
            }
            else
            {
                return Results.Conflict();
            }
            var obsolete = await db.Users.Where(user => user.PendingParentId == childId).ToListAsync(http.RequestAborted);
            foreach (var user in obsolete)
            {
                ClearInvitation(db, user);
            }
            ClearInvitation(db, parent);
        }
        ClearInvitation(db, child);
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.NoContent();
    }

    internal static void ClearInvitation(AbstractCoreContext db, HumanUser user)
    {
        db.Entry(user).Property(row => row.PendingLinkRequestId).CurrentValue = null;
        db.Entry(user).Property(row => row.PendingParentId).CurrentValue = null;
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> Release(Guid userId, HttpContext http, ISessionQueries sessions, AbstractCoreContext db, TimeProvider clock,
        IOptions<SecurityConfiguration> security)
    {
        var managers = await SubaccountEndpoints.GetFamilyManager(http, sessions, db, allowSwitchedPrimary: true);
        foreach (var manager in managers.AsEnumerable())
        {
            if (!await Roots(db).AnyAsync(user => EF.Property<Guid>(user, "Id") == manager.PrimaryUserId, http.RequestAborted))
            {
                return Results.Forbid();
            }
            var parentId = manager.PrimaryUserId;
            var link = await db.ActiveSubaccounts().SingleOrDefaultAsync(row => EF.Property<Guid>(row, "UserId") == userId
                && EF.Property<Guid>(row, "PrimaryUserId") == parentId && (manager.IsPrimary || row.IsChild), http.RequestAborted);
            if (link is null)
            {
                return Results.NotFound();
            }
            if (!await db.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == userId && user.Emails.Any(email =>
                    email.Value.Trim() != "" && (!security.Value.EmailConfirmation || email.ConfirmedAt != null)), http.RequestAborted))
            {
                return Results.Conflict();
            }
            var hasOtherLinks = await db.ActiveSubaccounts().AnyAsync(row => EF.Property<Guid>(row, "UserId") == userId
                && EF.Property<Guid>(row, "PrimaryUserId") != parentId, http.RequestAborted);
            var switched = await db.Sessions.Where(session => EF.Property<DateTimeOffset?>(session, "InvalidatedAt") == null
                && (manager.IsSwitched && EF.Property<Guid>(session, "Id") == manager.SessionId
                    || EF.Property<Guid?>(session, "FamilyPrimaryId") == parentId
                    || EF.Property<Guid?>(session, "FamilyManagerId") == parentId && EF.Property<Guid>(session, "UserId") == userId
                    || EF.Property<Guid?>(session, "FamilyPrimaryId") == userId
                    && (!hasOtherLinks || EF.Property<Guid>(session, "UserId") == parentId
                        || EF.Property<Guid?>(session, "FamilyManagerId") == parentId))).ToListAsync(http.RequestAborted);
            var now = clock.GetUtcNow();
            foreach (var session in switched)
            {
                db.Entry(session).Property<Guid?>("InvalidatedById").CurrentValue = manager.ActorId;
                db.Entry(session).Property<DateTimeOffset?>("InvalidatedAt").CurrentValue = now;
            }
            db.AppendSubaccountVersion(link, isActive: false);
            await db.SaveChangesAsync(http.RequestAborted);
            return Results.NoContent();
        }
        return Results.Forbid();
    }
}
