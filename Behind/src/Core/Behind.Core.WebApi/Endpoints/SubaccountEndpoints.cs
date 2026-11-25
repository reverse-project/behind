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

    private sealed record FamilySession(CurrentSession Current, SubaccountSwitchScope Scope, Guid PrimaryUserId, bool IsLinked, bool IsFamilyAdult)
    {
        public bool IsPrimary => Current.UserId == PrimaryUserId && !Scope.FromLinkedProfile;
        public bool CanManageChildren => IsPrimary || IsFamilyAdult;
        public SubaccountSwitchScope SwitchScope => Current.FamilyManagerUserId.Match(
            () => IsFamilyAdult && Scope.FromLinkedProfile ? new(PrimaryUserId, false) : Scope,
            managerId => new(managerId, false));
        public IQueryable<HumanUser> SwitchProfiles(AbstractCoreContext db) =>
            SwitchScope.Profiles(db).Union(Scope.Profiles(db));
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

        var canBrowseAndRelease = security.Value.Accounts.LinkUnlink && family.CanManageChildren;
        var isDirectSignIn = family.Current.FamilyPrimaryUserId == None;
        var requireConfirmation = security.Value.EmailConfirmation;
        List<Account> accounts = [];
        var primaryHasPin = !family.Scope.FromLinkedProfile && await PinsFor(db, family.PrimaryUserId).AnyAsync(http.RequestAborted);
        var currentHasPin = family.IsPrimary
            ? primaryHasPin
            : await PinsFor(db, family.Current.UserId).AnyAsync(http.RequestAborted);
        if (!family.Scope.FromLinkedProfile || family.IsFamilyAdult)
        {
            accounts = await (from link in db.Subaccounts
                              join user in ActiveUsers(db) on EF.Property<Guid>(link, "UserId") equals EF.Property<Guid>(user, "Id")
                              where EF.Property<Guid>(link, "PrimaryUserId") == family.PrimaryUserId && EF.Property<Guid>(link, "UserId") != family.Current.UserId
                                  && (!family.IsFamilyAdult || link.IsChild)
                              orderby user.DisplayName, EF.Property<Guid>(link, "UserId")
                              select new Account(EF.Property<Guid>(link, "UserId"), user.DisplayName, link.IsChild, family.CanManageChildren ? user.Birthday : null,
                                  db.UserSwitchPins.Any(pin => EF.Property<Guid>(pin, "UserId") == EF.Property<Guid>(link, "UserId")), family.CanManageChildren ? user.Emails.OrderBy(e => EF.Property<Guid>(e, "Id")).Select(e => e.Value).FirstOrDefault() : null,
                                  canBrowseAndRelease && (family.IsPrimary || link.IsChild) && user.Emails.Any(email => email.Value.Trim() != "" && (!requireConfirmation || email.ConfirmedAt != null))))
                .ToListAsync(http.RequestAborted);
        }
        if (!family.IsPrimary && !family.IsFamilyAdult && primaryHasPin)
        {
            var primary = await ActiveUsers(db).Where(user => EF.Property<Guid>(user, "Id") == family.PrimaryUserId)
                .Select(user => new Account(family.PrimaryUserId, user.DisplayName, false, null, true, null, false))
                .SingleOrDefaultAsync(http.RequestAborted);
            if (primary is not null)
            {
                accounts.Insert(0, primary);
            }
        }
        var switchAccounts = await (from user in family.SwitchProfiles(db)
                                    let userId = EF.Property<Guid>(user, "Id")
                                    let hasPin = db.UserSwitchPins.Any(pin => EF.Property<Guid>(pin, "UserId") == userId)
                                    let isChild = db.Subaccounts.Any(link => EF.Property<Guid>(link, "PrimaryUserId") == family.PrimaryUserId
                                        && EF.Property<Guid>(link, "UserId") == userId && link.IsChild)
                                    where userId != family.Current.UserId && (family.IsPrimary || hasPin || family.IsFamilyAdult && isChild)
                                    orderby user.DisplayName, userId
                                    select new Account(userId, user.DisplayName, db.Subaccounts.Any(link => EF.Property<Guid>(link, "UserId") == userId && link.IsChild),
                                        null, hasPin, null, false)).ToListAsync(http.RequestAborted);
        return Results.Ok(new
        {
            CanRespondToInvitations = security.Value.Accounts.LinkUnlink && isDirectSignIn,
            family.IsLinked,
            family.CanManageChildren,
            SwitchAccounts = switchAccounts,
            CanBrowseAndRelease = canBrowseAndRelease,
            LinkUnlinkEnabled = canBrowseAndRelease && (isDirectSignIn || family.IsFamilyAdult),
            LinkUnlinkRequiresDirectSignIn = canBrowseAndRelease && !isDirectSignIn && !family.IsFamilyAdult,
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
        if (family is not { CanManageChildren: true })
        {
            return Results.Forbid();
        }
        if (!family.IsPrimary && (!request.IsChild || !string.IsNullOrEmpty(request.PrimaryPin)))
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
            && !await VerifyProof(request.Proof, family, family.Current.UserId, PinProofOperation.Create, db, http))
        {
            return Results.Forbid();
        }

        if (HashPin(request.PrimaryPin) is not null)
        {
            if (await PinsFor(db, family.PrimaryUserId).AnyAsync(http.RequestAborted))
            {
                return Results.Conflict();
            }
            await ApplyPin(family.PrimaryUserId, request.PrimaryPin, primary, db, http.RequestAborted);
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
        if (family is not { CanManageChildren: true })
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
        if (!family.IsPrimary && (!target.Link.IsChild || !request.IsChild))
        {
            return Results.Forbid();
        }
        if (target.Link.IsChild && !request.IsChild
            && await db.Subaccounts.AnyAsync(link =>
                EF.Property<Guid>(link, "UserId") == userId
                && EF.Property<Guid>(link, "PrimaryUserId") != family.PrimaryUserId, http.RequestAborted))
        {
            return Results.Conflict();
        }
        if (request.ClearPin || !string.IsNullOrEmpty(request.Pin))
        {
            if (!await VerifyProof(request.Proof, family, userId,
                    request.ClearPin ? PinProofOperation.Remove : PinProofOperation.Change, db, http))
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
        return family is { CanManageChildren: true } && await ManagedUsers(db, family, targetId).AnyAsync(http.RequestAborted)
            ? family.Current.UserId
            : null;
    }

    private static IQueryable<HumanUser> ManagedUsers(AbstractCoreContext db, FamilySession family, Guid targetId) =>
        ActiveUsers(db).Where(user => EF.Property<Guid>(user, "Id") == targetId && (family.Current.UserId == targetId ||
            db.Subaccounts.Any(link => EF.Property<Guid>(link, "PrimaryUserId") == family.PrimaryUserId
                && EF.Property<Guid>(link, "UserId") == targetId && (family.IsPrimary || link.IsChild))));

    [Authorize, IgnoreETag]
    public static async Task<IResult> ChangePin(Guid userId, PinRequest request, HttpContext http, ISessionQueries sessions, AbstractCoreContext db)
    {
        var family = await FindFamily(http, sessions, db);
        if (family is null || (!family.CanManageChildren && (!family.IsLinked || family.Current.UserId != userId)))
        {
            return Results.Forbid();
        }
        if (request.ClearPin ? request.Pin is not null : string.IsNullOrEmpty(request.Pin))
        {
            return Results.BadRequest();
        }
        var target = await (family.CanManageChildren
            ? ManagedUsers(db, family, userId)
            : ActiveUsers(db).Where(user => EF.Property<Guid>(user, "Id") == userId)).SingleOrDefaultAsync(http.RequestAborted);
        if (target is null)
        {
            return Results.NotFound();
        }
        if (family.IsLinked && family.Current.UserId == userId && request.Proof is null && !request.ClearPin)
        {
            if (await PinsFor(db, userId).AnyAsync(http.RequestAborted))
            {
                return Results.Forbid();
            }
            await ApplyPin(userId, request.Pin, target, db, http.RequestAborted);
            return await SavePinChanges(db, http);
        }
        if (!await VerifyProof(request.Proof, family, userId,
                request.ClearPin ? PinProofOperation.Remove : PinProofOperation.Change, db, http))
        {
            return Results.Forbid();
        }
        await ApplyPin(userId, request.Pin, target, db, http.RequestAborted);
        return await SavePinChanges(db, http);
    }

    private static async Task<bool> VerifyProof(
        PinProof? proof, FamilySession family, Guid targetId, PinProofOperation operation,
        AbstractCoreContext db, HttpContext http)
    {
        (Guid UserId, string Pin)? pinProof = proof switch
        {
            { CurrentPin: { Length: > 0 } pin, PrimaryPin: null, Password: null, MagicToken: null }
                when family.IsPrimary || targetId == family.Current.UserId => (targetId, pin),
            { PrimaryPin: { Length: > 0 } pin, CurrentPin: null, Password: null, MagicToken: null }
                when family.CanManageChildren && targetId != family.Current.UserId
                    && operation is PinProofOperation.Change or PinProofOperation.Remove => (family.Current.UserId, pin),
            _ => null
        };
        if (pinProof is { } credential)
        {
            var hash = await PinsFor(db, credential.UserId).SingleOrDefaultAsync(http.RequestAborted);
            return hash is not null && PasswordHash.CreateFromExisting(hash.PinHash.ToString()).Verify(credential.Pin);
        }
        if (!family.CanManageChildren || (!family.IsPrimary && targetId == family.Current.UserId && operation != PinProofOperation.Create))
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
                using (db.SoftDelete.IncludeDeleted())
                {
                    credential = await PinsFor(db, userId).SingleOrDefaultAsync(cancellationToken);
                    if (credential is null)
                    {
                        db.UserSwitchPins.Add(new UserSwitchPin(user, hash));
                    }
                    else
                    {
                        db.Restores.Mark(credential);
                        db.Entry(credential).Property(record => record.PinHash).CurrentValue = hash;
                    }
                }
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
        if (family is null || family.Current.UserId == userId)
        {
            return Results.Forbid();
        }
        if (!await family.SwitchProfiles(db).AnyAsync(user => EF.Property<Guid>(user, "Id") == userId, http.RequestAborted))
        {
            return Results.NotFound();
        }
        var credential = await PinsFor(db, userId).AsNoTracking().SingleOrDefaultAsync(http.RequestAborted);
        var isChild = await db.Subaccounts.AnyAsync(link => EF.Property<Guid>(link, "PrimaryUserId") == family.PrimaryUserId
            && EF.Property<Guid>(link, "UserId") == userId && link.IsChild, http.RequestAborted);
        if (credential is null
            ? !(family.IsPrimary || family.IsFamilyAdult && isChild)
            : string.IsNullOrEmpty(request.Pin) || !PasswordHash.CreateFromExisting(credential.PinHash.ToString()).Verify(request.Pin))
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
                var managerId = family.Current.FamilyManagerUserId;
                if (family.Scope.FromLinkedProfile && managerId == None && family.IsFamilyAdult)
                {
                    managerId = family.PrimaryUserId;
                }
                if (family.Scope.FromLinkedProfile
                    && await db.Subaccounts.AnyAsync(link => EF.Property<Guid>(link, "PrimaryUserId") == userId
                        && EF.Property<Guid>(link, "UserId") == family.Scope.AnchorId, http.RequestAborted))
                {
                    managerId = userId;
                }
                var response = await SignInEndpoints.StartSession(user, settings, false, commands, bans, now, http,
                    logger, None, familyPrimaryUserId: family.Scope.AnchorId, familyManagerUserId: managerId);
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
        var linked = await SubaccountSwitchScope.IsLinked(db, session.UserId, http.RequestAborted);
        var anchorId = session.FamilyPrimaryUserId.GetOrElse(session.UserId);
        var fromLinkedProfile = anchorId == session.UserId
            ? linked
            : await SubaccountSwitchScope.IsLinked(db, anchorId, http.RequestAborted);
        var scope = new SubaccountSwitchScope(anchorId, fromLinkedProfile);
        var managerId = session.FamilyManagerUserId.ToNullable();
        if (managerId is { } manager
            && (!fromLinkedProfile || !await db.Subaccounts.AnyAsync(link =>
                EF.Property<Guid>(link, "PrimaryUserId") == manager && EF.Property<Guid>(link, "UserId") == anchorId,
                http.RequestAborted)))
        {
            return null;
        }
        var authorizedScope = managerId is { } effectiveManager ? new SubaccountSwitchScope(effectiveManager, false) : scope;
        if (anchorId != session.UserId
            && !await authorizedScope.Profiles(db).AnyAsync(user => EF.Property<Guid>(user, "Id") == session.UserId, http.RequestAborted))
        {
            return null;
        }
        var isFamilyAdult = session.FamilyPrimaryUserId != None && (
            managerId is { } familyManager && session.UserId != anchorId
                && (session.UserId == familyManager || await db.Subaccounts.AnyAsync(link =>
                    EF.Property<Guid>(link, "PrimaryUserId") == familyManager
                    && EF.Property<Guid>(link, "UserId") == session.UserId && !link.IsChild, http.RequestAborted))
            || managerId is null && (
            fromLinkedProfile && anchorId != session.UserId && !linked
            || !fromLinkedProfile && anchorId != session.UserId
                && await db.Subaccounts.AnyAsync(link => EF.Property<Guid>(link, "PrimaryUserId") == anchorId
                    && EF.Property<Guid>(link, "UserId") == session.UserId && !link.IsChild, http.RequestAborted)));
        var primaryId = managerId ?? (isFamilyAdult && fromLinkedProfile
            ? session.UserId
            : fromLinkedProfile && linked
            ? await db.Subaccounts.Where(link => EF.Property<Guid>(link, "UserId") == session.UserId)
                .OrderBy(link => EF.Property<Guid>(link, "PrimaryUserId"))
                .Select(link => (Guid?)EF.Property<Guid>(link, "PrimaryUserId")).FirstOrDefaultAsync(http.RequestAborted) ?? anchorId
            : anchorId);
        return new(session, scope, primaryId, linked, isFamilyAdult);
    }

    internal sealed record FamilyManager(Guid PrimaryUserId, bool IsPrimary, Guid ActorId, Guid SessionId, bool IsSwitched);

    internal static async Task<Option<FamilyManager>> GetFamilyManager(HttpContext http, ISessionQueries sessions, AbstractCoreContext db,
        bool allowSwitchedPrimary)
    {
        var family = await FindFamily(http, sessions, db);
        return family is { CanManageChildren: true }
            && (family.IsFamilyAdult || family.Current.FamilyPrimaryUserId == None || allowSwitchedPrimary)
            ? Some(new FamilyManager(family.PrimaryUserId, family.IsPrimary, family.Current.UserId, family.Current.SessionId,
                family.Current.FamilyPrimaryUserId != None))
            : None;
    }
}
