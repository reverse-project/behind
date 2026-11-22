using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SubaccountEndpoints
{
    private const string PinPattern = "^[0-9]+$";

    public sealed record CreateRequest(
        [property: Required, MaxLength(DisplayNameExtensions.MaxLength)] string DisplayName,
        [property: EmailAddress, MaxLength(255)] string? Email,
        DateOnly? Birthday, bool IsChild,
        [property: RegularExpression(PinPattern), MaxLength(128)] string? Pin);

    public sealed record UpdateRequest(
        [property: Required, MaxLength(DisplayNameExtensions.MaxLength)] string DisplayName,
        DateOnly? Birthday, bool IsChild,
        [property: RegularExpression(PinPattern), MaxLength(128)] string? Pin, bool ClearPin);

    public sealed record SwitchRequest([property: MaxLength(128)] string? Pin);

    public sealed record Account(Guid UserId, string DisplayName, bool IsChild, DateOnly? Birthday, bool HasPin, string? Email);

    private sealed record FamilySession(CurrentSession Current, Guid PrimaryUserId, bool CanSwitch)
    {
        public bool IsPrimary => Current.UserId == PrimaryUserId;
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> List(HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var family = await FindFamily(http, sessions, db);
        if (family is null)
        {
            return Results.Forbid();
        }

        List<Account> accounts = [];
        if (family.CanSwitch)
        {
            accounts = await (from link in db.Subaccounts
                              join user in ActiveUsers(db) on link.UserId equals EF.Property<Guid>(user, "Id")
                              where link.PrimaryUserId == family.PrimaryUserId && link.UserId != family.Current.UserId
                              orderby user.DisplayName, link.UserId
                              select new Account(link.UserId, user.DisplayName, link.IsChild, family.IsPrimary ? user.Birthday : null,
                                  link.PinHash != null, family.IsPrimary ? user.Emails.OrderBy(e => EF.Property<Guid>(e, "Id")).Select(e => e.Value).FirstOrDefault() : null))
                .ToListAsync(http.RequestAborted);
        }
        return Results.Ok(new { family.IsPrimary, family.PrimaryUserId, Accounts = accounts });
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> Create(CreateRequest request, HttpContext http, ISessionQueries sessions,
        AbstractCoreContext db, IRoleCommands roles, TimeProvider clock)
    {
        var family = await FindFamily(http, sessions, db);
        if (family is not { IsPrimary: true })
        {
            return Results.Forbid();
        }
        if (request.Birthday > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
        {
            return Results.BadRequest();
        }
        var email = request.Email?.Trim().ToLowerInvariant();
        if (email is not null && await db.Emails.AnyAsync(e => e.Value.ToLower() == email, http.RequestAborted))
        {
            return Results.Conflict();
        }

        var user = new HumanUser
        {
            DisplayName = request.DisplayName.Trim(),
            Birthday = request.Birthday,
            Emails = email is null ? [] : [new Email(email)]
        };
        db.Users.Add(user);
        var userId = db.GetKey(user);
        var link = new Subaccount(family.PrimaryUserId, userId, request.IsChild, HashPin(request.Pin));
        db.Subaccounts.Add(link);
        await db.SaveChangesAsync(http.RequestAborted);
        await roles.AssignBuiltIn(userId, BuiltInAccess.Users, http.RequestAborted);
        return Results.Created($"/profile/subaccounts/{userId}", new { UserId = userId });
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> Update(Guid userId, UpdateRequest request, HttpContext http,
        ISessionQueries sessions, AbstractCoreContext db, TimeProvider clock)
    {
        var family = await FindFamily(http, sessions, db);
        if (family is not { IsPrimary: true })
        {
            return Results.Forbid();
        }
        if ((request.ClearPin && request.Pin is not null) || request.Birthday > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
        {
            return Results.BadRequest();
        }
        var target = await (from link in db.Subaccounts
                            join user in ActiveUsers(db) on link.UserId equals EF.Property<Guid>(user, "Id")
                            where link.PrimaryUserId == family.PrimaryUserId && link.UserId == userId
                            select new { Link = link, User = user }).SingleOrDefaultAsync(http.RequestAborted);
        if (target is null)
        {
            return Results.NotFound();
        }
        db.Entry(target.User).Property(u => u.DisplayName).CurrentValue = request.DisplayName.Trim();
        db.Entry(target.User).Property(u => u.Birthday).CurrentValue = request.Birthday;
        db.Entry(target.Link).Property(l => l.IsChild).CurrentValue = request.IsChild;
        if (request.ClearPin || !string.IsNullOrEmpty(request.Pin))
        {
            db.Entry(target.Link).Property(l => l.PinHash).CurrentValue = HashPin(request.Pin);
        }
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.NoContent();
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> Switch(Guid userId, SwitchRequest request, HttpContext http,
        ISessionQueries sessions, AbstractCoreContext db, UserManager<ApplicationUser> users,
        IOptions<SecurityConfiguration> security, ISessionCommands commands, IWallOfShameQueries bans,
        TimeProvider clock, ILogger<SubaccountEndpoints> logger)
    {
        var family = await FindFamily(http, sessions, db);
        if (family is not { CanSwitch: true } || family.Current.UserId == userId)
        {
            return Results.Forbid();
        }
        var link = await db.Subaccounts.AsNoTracking().SingleOrDefaultAsync(l => l.PrimaryUserId == family.PrimaryUserId
            && l.UserId == userId, http.RequestAborted);
        if (link is null || !await ActiveUsers(db).AnyAsync(user => EF.Property<Guid>(user, "Id") == userId, http.RequestAborted))
        {
            return Results.NotFound();
        }
        if (link.PinHash is not null && (string.IsNullOrEmpty(request.Pin)
            || !PasswordHash.CreateFromExisting(link.PinHash).Verify(request.Pin)))
        {
            return Results.Forbid();
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }
        var now = clock.GetUtcNow();
        return await security.Value.Jwt.Match(
            () => Task.FromResult<IResult>(Results.InternalServerError()),
            async settings =>
            {
                var response = await SignInEndpoints.StartSession(user, settings, false, commands, bans, now, http,
                    logger, None, familyPrimaryUserId: family.PrimaryUserId);
                if (response is IValueHttpResult { Value: SignInEndpoints.SignInResponse })
                {
                    await commands.InvalidateSession(family.Current.SessionId, family.Current.UserId, now, http.RequestAborted);
                }
                return response;
            });
    }

    private static IQueryable<HumanUser> ActiveUsers(AbstractCoreContext db) =>
        db.Users.InDeletionState(db, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

    private static string? HashPin(string? pin) => string.IsNullOrEmpty(pin) ? null : PasswordHash.Create(pin).ToString();

    private static async Task<FamilySession?> FindFamily(HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        http.Response.Headers.CacheControl = "no-store";
        var session = (await http.FindCurrentSession(sessions)).Match<CurrentSession?>(() => null, value => value);
        if (session is null || session.IsImpersonated)
        {
            return null;
        }
        var parentId = await db.Subaccounts.Where(link => link.UserId == session.UserId)
            .Select(link => (Guid?)link.PrimaryUserId).SingleOrDefaultAsync(http.RequestAborted);
        if (parentId is null)
        {
            return new(session, session.UserId, true);
        }
        var switched = await db.Sessions.AnyAsync(row => EF.Property<Guid>(row, "Id") == session.SessionId
            && row.FamilyPrimaryUserId == parentId, http.RequestAborted);
        return new(session, parentId.Value, switched);
    }
}
