using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Microsoft.Extensions.DependencyInjection;
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

    public interface IPinRequest
    {
        string? Pin { get; }
        string? PrimaryPin => null;
    }

    public sealed record PinProof(string? CurrentPin = null, string? Password = null, string? MagicToken = null, string? PrimaryPin = null);

    public sealed record PinRequest(
        [property: RegularExpression(PinPattern), MaxLength(SecurityAccountSettings.DefaultSwitchPinMaxLength)] string? Pin,
        bool ClearPin, PinProof? Proof = null) : IPinRequest;

    public sealed record CreateRequest(
        [property: Required, MaxLength(DisplayNameExtensions.MaxLength)] string DisplayName,
        [property: EmailAddress, MaxLength(255)] string? Email,
        DateOnly? Birthday, bool IsChild,
        [property: RegularExpression(PinPattern), MaxLength(SecurityAccountSettings.DefaultSwitchPinMaxLength)] string? Pin,
        [property: RegularExpression(PinPattern), MaxLength(SecurityAccountSettings.DefaultSwitchPinMaxLength)] string? PrimaryPin = null, PinProof? Proof = null) : IPinRequest;

    public sealed record UpdateRequest(
        [property: Required, MaxLength(DisplayNameExtensions.MaxLength)] string DisplayName,
        DateOnly? Birthday, bool IsChild,
        [property: RegularExpression(PinPattern), MaxLength(SecurityAccountSettings.DefaultSwitchPinMaxLength)] string? Pin, bool ClearPin, PinProof? Proof = null) : IPinRequest;

    public sealed record SwitchRequest([property: MaxLength(SecurityAccountSettings.DefaultSwitchPinMaxLength)] string? Pin) : IPinRequest;

    public sealed record Account(Guid UserId, string DisplayName, bool IsChild, DateOnly? Birthday, bool HasPin, string? Email, bool CanRelease);

    private sealed record FamilySession(CurrentSession Current, Guid PrimaryUserId, bool CanSwitch)
    {
        public bool IsPrimary => Current.UserId == PrimaryUserId;
    }

    [Authorize, IgnoreETag]
    public static async Task<IResult> List(HttpContext http, ISessionQueries sessions, AbstractCoreContext db,
        IOptions<SecurityConfiguration> security)
    {
        var family = await FindFamily(http, sessions, db);
        if (family is null)
        {
            return Results.Forbid();
        }

        var canManageLinks = security.Value.Accounts.LinkUnlink && family.IsPrimary;
        var isDirectSignIn = family.Current.FamilyPrimaryUserId is null;
        var linkUnlinkEnabled = canManageLinks && isDirectSignIn;
        var requireConfirmation = security.Value.EmailConfirmation;
        List<Account> accounts = [];
        var primaryHasPin = family.CanSwitch && await PinsFor(db, family.PrimaryUserId).AnyAsync(http.RequestAborted);
        var currentHasPin = family.IsPrimary
            ? primaryHasPin
            : await PinsFor(db, family.Current.UserId).AnyAsync(http.RequestAborted);
        if (family.CanSwitch)
        {
            accounts = await (from link in db.Subaccounts
                              join user in ActiveUsers(db) on EF.Property<Guid>(link, "UserId") equals EF.Property<Guid>(user, "Id")
                              where EF.Property<Guid>(link, "PrimaryUserId") == family.PrimaryUserId && EF.Property<Guid>(link, "UserId") != family.Current.UserId
                              orderby user.DisplayName, EF.Property<Guid>(link, "UserId")
                              select new Account(EF.Property<Guid>(link, "UserId"), user.DisplayName, link.IsChild, family.IsPrimary ? user.Birthday : null,
                                  db.UserSwitchPins.Any(pin => EF.Property<Guid>(pin, "UserId") == EF.Property<Guid>(link, "UserId")), family.IsPrimary ? user.Emails.OrderBy(e => EF.Property<Guid>(e, "Id")).Select(e => e.Value).FirstOrDefault() : null,
                                  linkUnlinkEnabled && user.Emails.Any(email => email.Value.Trim() != "" && (!requireConfirmation || email.ConfirmedAt != null))))
                .ToListAsync(http.RequestAborted);
        }
        if (!family.IsPrimary && primaryHasPin)
        {
            var primary = await ActiveUsers(db).Where(user => EF.Property<Guid>(user, "Id") == family.PrimaryUserId)
                .Select(user => new Account(family.PrimaryUserId, user.DisplayName, false, null, true, null, false))
                .SingleOrDefaultAsync(http.RequestAborted);
            if (primary is not null)
            {
                accounts.Insert(0, primary);
            }
        }
        return Results.Ok(new
        {
            LinkUnlinkEnabled = linkUnlinkEnabled,
            LinkUnlinkRequiresDirectSignIn = canManageLinks && !isDirectSignIn,
            family.IsPrimary,
            family.PrimaryUserId,
            PrimaryHasPin = primaryHasPin,
            CurrentHasPin = currentHasPin,
            Accounts = accounts
        });
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

        var primary = await ActiveUsers(db).SingleOrDefaultAsync(
            user => EF.Property<Guid>(user, "Id") == family.PrimaryUserId, http.RequestAborted);
        if (primary is null)
        {
            return Results.Forbid();
        }

        if ((!string.IsNullOrEmpty(request.Pin) || !string.IsNullOrEmpty(request.PrimaryPin))
            && !await VerifyProof(request.Proof, family, family.PrimaryUserId, "create", db, http))
        {
            return Results.Forbid();
        }

        if (HashPin(request.PrimaryPin) is { } primaryPin)
        {
            if (await PinsFor(db, family.PrimaryUserId).AnyAsync(http.RequestAborted))
            {
                return Results.Conflict();
            }
            db.UserSwitchPins.Add(new UserSwitchPin(primary, primaryPin));
        }

        var user = new HumanUser
        {
            DisplayName = request.DisplayName.Trim(),
            Birthday = request.Birthday,
            Emails = email is null ? [] : [new Email(email)]
        };
        SubaccountLinkEndpoints.ClearInvitation(db, primary);
        db.Users.Add(user);
        var userId = db.GetKey(user);
        db.Subaccounts.Add(new Subaccount(primary, user, request.IsChild));
        if (HashPin(request.Pin) is { } childPin)
        {
            db.UserSwitchPins.Add(new UserSwitchPin(user, childPin));
        }
        try
        {
            await db.SaveChangesAsync(http.RequestAborted);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.Matches(exception, "PK_UserSwitchPins", "UserSwitchPins.UserId"))
        {
            return Results.Conflict();
        }
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
                            join user in ActiveUsers(db) on EF.Property<Guid>(link, "UserId") equals EF.Property<Guid>(user, "Id")
                            where EF.Property<Guid>(link, "PrimaryUserId") == family.PrimaryUserId && EF.Property<Guid>(link, "UserId") == userId
                            select new { Link = link, User = user }).SingleOrDefaultAsync(http.RequestAborted);
        if (target is null)
        {
            return Results.NotFound();
        }
        if (request.ClearPin || !string.IsNullOrEmpty(request.Pin))
        {
            if (!await VerifyProof(request.Proof, family, userId, request.ClearPin ? "remove" : "change", db, http))
            {
                return Results.Forbid();
            }
            await ApplyPin(userId, request.Pin, target.User, db, http.RequestAborted);
        }
        db.Entry(target.User).Property(u => u.DisplayName).CurrentValue = request.DisplayName.Trim();
        db.Entry(target.User).Property(u => u.Birthday).CurrentValue = request.Birthday;
        db.Entry(target.Link).Property(l => l.IsChild).CurrentValue = request.IsChild;
        return await SavePinChanges(db, http);
    }

    internal static async Task<Guid?> GetPinManager(Guid targetId, HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var family = await FindFamily(http, sessions, db);
        return family is { IsPrimary: true } && await ManagedUsers(db, family.PrimaryUserId, targetId).AnyAsync(http.RequestAborted)
            ? family.Current.UserId
            : null;
    }

    private static IQueryable<HumanUser> ManagedUsers(AbstractCoreContext db, Guid primaryId, Guid targetId) =>
        ActiveUsers(db).Where(user => EF.Property<Guid>(user, "Id") == targetId && (primaryId == targetId ||
            db.Subaccounts.Any(link => EF.Property<Guid>(link, "PrimaryUserId") == primaryId && EF.Property<Guid>(link, "UserId") == targetId)));

    [Authorize, IgnoreETag]
    public static async Task<IResult> ChangePin(Guid userId, PinRequest request, HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var family = await FindFamily(http, sessions, db);
        if (family is null || (!family.IsPrimary && family.Current.UserId != userId))
        {
            return Results.Forbid();
        }
        if (request.ClearPin ? request.Pin is not null : string.IsNullOrEmpty(request.Pin))
        {
            return Results.BadRequest();
        }
        var target = await ManagedUsers(db, family.PrimaryUserId, userId).SingleOrDefaultAsync(http.RequestAborted);
        if (target is null)
        {
            return Results.NotFound();
        }
        if (!family.IsPrimary && request.Proof is null && !request.ClearPin)
        {
            if (await PinsFor(db, userId).AnyAsync(http.RequestAborted))
            {
                return Results.Forbid();
            }
            db.UserSwitchPins.Add(new UserSwitchPin(target, HashPin(request.Pin)!));
            return await SavePinChanges(db, http);
        }
        if (!await VerifyProof(request.Proof, family, userId, request.ClearPin ? "remove" : "change", db, http))
        {
            return Results.Forbid();
        }
        await ApplyPin(userId, request.Pin, target, db, http.RequestAborted);
        return await SavePinChanges(db, http);
    }

    private static async Task<bool> VerifyProof(PinProof? proof, FamilySession family, Guid targetId, string operation,
        AbstractCoreContext db, HttpContext http)
    {
        (Guid UserId, string Pin)? pinProof = proof switch
        {
            { CurrentPin: { Length: > 0 } pin, PrimaryPin: null, Password: null, MagicToken: null } => (targetId, pin),
            { PrimaryPin: { Length: > 0 } pin, CurrentPin: null, Password: null, MagicToken: null }
                when family.IsPrimary && targetId != family.PrimaryUserId && operation is "change" or "remove" => (family.PrimaryUserId, pin),
            _ => null
        };
        if (pinProof is { } credential)
        {
            var hash = await PinsFor(db, credential.UserId).SingleOrDefaultAsync(http.RequestAborted);
            return hash is not null && PasswordHash.CreateFromExisting(hash.PinHash.ToString()).Verify(credential.Pin);
        }
        if (!family.IsPrimary)
        {
            return false;
        }
        if (proof is { Password: { Length: > 0 } password, CurrentPin: null, MagicToken: null, PrimaryPin: null })
        {
            if (!http.RequestServices.GetRequiredService<IOptionsSnapshot<AccountsConfiguration>>().Value.Password)
            {
                return false;
            }
            var hash = await db.Passwords.Where(record => record.IsActive && EF.Property<Guid>(record, "UserId") == family.Current.UserId)
                .Select(record => record.Value).FirstOrDefaultAsync(http.RequestAborted);
            return hash is not null && PasswordHash.CreateFromExisting(hash).Verify(password);
        }
        return proof is { MagicToken: { Length: > 0 } token, CurrentPin: null, Password: null, PrimaryPin: null }
            && await PinMagicProof.Consume(token, family.Current.UserId, targetId, operation, http);
    }

    private static async Task ApplyPin(Guid userId, string? pin, HumanUser user, AbstractCoreContext db, CancellationToken cancellationToken)
    {
        var credential = await PinsFor(db, userId).SingleOrDefaultAsync(cancellationToken);
        if (HashPin(pin) is { } hash)
        {
            if (credential is null)
            {
                db.UserSwitchPins.Add(new UserSwitchPin(user, hash));
            }
            else
            {
                db.Entry(credential).Property(record => record.PinHash).CurrentValue = hash;
            }
        }
        else if (credential is not null)
        {
            db.UserSwitchPins.Remove(credential);
        }
    }

    private static async Task<IResult> SavePinChanges(AbstractCoreContext db, HttpContext http)
    {
        try
        {
            await db.SaveChangesAsync(http.RequestAborted);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.Matches(exception, "PK_UserSwitchPins", "UserSwitchPins.UserId"))
        {
            return Results.Conflict();
        }
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
        var isPrimaryTarget = userId == family.PrimaryUserId;
        if (!isPrimaryTarget && !await db.Subaccounts.AnyAsync(link => EF.Property<Guid>(link, "PrimaryUserId") == family.PrimaryUserId
            && EF.Property<Guid>(link, "UserId") == userId, http.RequestAborted))
        {
            return Results.NotFound();
        }
        if (!await ActiveUsers(db).AnyAsync(user => EF.Property<Guid>(user, "Id") == userId, http.RequestAborted))
        {
            return Results.NotFound();
        }
        var credential = await PinsFor(db, userId).AsNoTracking().SingleOrDefaultAsync(http.RequestAborted);
        if (credential is null && isPrimaryTarget)
        {
            return Results.Forbid();
        }
        if (credential is not null && (string.IsNullOrEmpty(request.Pin)
            || !PasswordHash.CreateFromExisting(credential.PinHash.ToString()).Verify(request.Pin)))
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

    private static IQueryable<UserSwitchPin> PinsFor(AbstractCoreContext db, Guid userId) =>
        db.UserSwitchPins.Where(pin => EF.Property<Guid>(pin, "UserId") == userId);

    private static PinHash? HashPin(string? pin) => string.IsNullOrEmpty(pin) ? null : new PinHash(PasswordHash.Create(pin).ToString());

    private static async Task<FamilySession?> FindFamily(HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        http.Response.Headers.CacheControl = "no-store";
        var session = (await http.FindCurrentSession(sessions)).Match<CurrentSession?>(() => null, value => value);
        if (session is null || session.IsImpersonated)
        {
            return null;
        }
        var parentId = await FindPrimaryUserId(db, session.UserId, http.RequestAborted);
        if (parentId is null)
        {
            return session.FamilyPrimaryUserId is { } source && source != session.UserId ? null : new(session, session.UserId, true);
        }
        return new(session, parentId.Value, session.FamilyPrimaryUserId == parentId);
    }

    private static Task<Guid?> FindPrimaryUserId(AbstractCoreContext db, Guid userId, CancellationToken cancellationToken) =>
        db.Subaccounts.Where(link => EF.Property<Guid>(link, "UserId") == userId)
            .Select(link => (Guid?)EF.Property<Guid>(link, "PrimaryUserId")).SingleOrDefaultAsync(cancellationToken);
}
