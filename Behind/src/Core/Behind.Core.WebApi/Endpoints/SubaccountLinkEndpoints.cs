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

    private static IQueryable<HumanUser> Roots(AbstractCoreContext db) =>
        db.Users.InDeletionState(db, AbstractCoreContext.DeletionRecorded, DeletionState.Active)
            .Where(user => !db.Subaccounts.Any(link => EF.Property<Guid>(link, "UserId") == EF.Property<Guid>(user, "Id")));

    private static IQueryable<HumanUser> EligibleChildren(AbstractCoreContext db) =>
        Roots(db).Where(user => user.Emails.Any(email => email.Value.Trim() != "")
                                && !db.Subaccounts.Any(link => EF.Property<Guid>(link, "PrimaryUserId") == EF.Property<Guid>(user, "Id")));

    private static async Task<HumanUser?> Actor(HttpContext http, ISessionQueries sessions, AbstractCoreContext db,
        bool allowSwitchedPrimary = false)
    {
        http.Response.Headers.CacheControl = "no-store";
        var actor = (await http.FindCurrentSession(sessions)).Match<CurrentSession?>(() => null, value => value);
        if (actor is null || actor.IsImpersonated
            || actor.FamilyPrimaryUserId is { } familyId && (!allowSwitchedPrimary || familyId != actor.UserId))
        {
            return null;
        }
        return await Roots(db).SingleOrDefaultAsync(user => EF.Property<Guid>(user, "Id") == actor.UserId, http.RequestAborted);
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> Candidates(string? query, HttpContext http, ISessionQueries sessions, AbstractCoreContext db,
        int? page = null, int? pageSize = null)
    {
        var parent = await Actor(http, sessions, db, allowSwitchedPrimary: true);
        if (parent is null)
        {
            return Results.Forbid();
        }
        var parentId = db.GetKey(parent);
        var search = query?.Trim().ToLowerInvariant() ?? "";
        var pagination = Flipper.Create(page, pageSize, ManagedListExtensions.DefaultPageSize, ManagedListExtensions.MaxPageSize);
        var candidates = EligibleChildren(db).Where(user => EF.Property<Guid>(user, "Id") != parentId
                && user.PendingLinkRequestId == null && user.DisplayName.ToLower().Contains(search))
            .OrderBy(user => user.DisplayName).ThenBy(user => EF.Property<Guid>(user, "Id"));
        var rows = await pagination.Apply(candidates).Select(user => new Candidate(EF.Property<Guid>(user, "Id"), user.DisplayName)).ToListAsync(http.RequestAborted);
        return Results.Ok(rows.Select(row => row with { DisplayName = row.UserId.ToUserLabel(row.DisplayName) }));
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> RequestLink(Guid userId, HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var parent = await Actor(http, sessions, db);
        if (parent is null)
        {
            return Results.Forbid();
        }
        var parentId = db.GetKey(parent);
        var child = await EligibleChildren(db).SingleOrDefaultAsync(user => EF.Property<Guid>(user, "Id") == userId,
            http.RequestAborted);
        if (child is null || parentId == userId || child.PendingLinkRequestId.HasValue)
        {
            return Results.Conflict();
        }
        db.Entry(child).Property(user => user.PendingParentId).CurrentValue = parentId;
        db.Entry(child).Property(user => user.PendingLinkRequestId).CurrentValue = Guid.NewGuid();
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.NoContent();
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> Requests(HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var child = await Actor(http, sessions, db);
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
        var child = await Actor(http, sessions, db);
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
            if (parent is null || childId == parentId ||
                !await EligibleChildren(db).AnyAsync(user => EF.Property<Guid>(user, "Id") == childId, http.RequestAborted))
            {
                return Results.Conflict();
            }
            db.Subaccounts.Add(new Subaccount(parent, child, true));
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
        var parent = await Actor(http, sessions, db, allowSwitchedPrimary: true);
        if (parent is null)
        {
            return Results.Forbid();
        }
        var parentId = db.GetKey(parent);
        var link = await db.Subaccounts.SingleOrDefaultAsync(row => EF.Property<Guid>(row, "UserId") == userId
            && EF.Property<Guid>(row, "PrimaryUserId") == parentId, http.RequestAborted);
        if (link is null)
        {
            return Results.NotFound();
        }
        if (!await db.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == userId && user.Emails.Any(email =>
                email.Value.Trim() != "" && (!security.Value.EmailConfirmation || email.ConfirmedAt != null)), http.RequestAborted))
        {
            return Results.Conflict();
        }
        var switched = await db.Sessions.Where(session => session.FamilyPrimaryUserId == parentId
            && EF.Property<DateTimeOffset?>(session, "InvalidatedAt") == null).ToListAsync(http.RequestAborted);
        var now = clock.GetUtcNow();
        foreach (var session in switched)
        {
            db.Entry(session).Property<Guid?>("InvalidatedById").CurrentValue = parentId;
            db.Entry(session).Property<DateTimeOffset?>("InvalidatedAt").CurrentValue = now;
        }
        db.Subaccounts.Remove(link);
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.NoContent();
    }
}
