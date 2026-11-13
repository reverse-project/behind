using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class PublicProfileEndpoints
{
    public sealed record ProfilePermissions(bool CanModerate, bool CanImpersonate, bool CanSendPrivateMessage = false);

    public sealed record DetailsRequest(DateOnly? Birthday, bool? HideEmail);

    public sealed record ModerationDetails(
        string DisplayName, DateOnly? Birthday, bool HideEmail, DateTimeOffset? UpdatedAt,
        IReadOnlyList<IEmailQueries.Address> Emails,
        IReadOnlyList<UserProfiles.BanRecord> Bans, IReadOnlyList<string> EffectiveGrants,
        IReadOnlyList<string> DirectGrants, IReadOnlyList<string> DirectRestrictions,
        bool TotpEnabled, int PasskeyCount, int RecoveryCodesRemaining);

    public sealed record ProfileResponse(
        Guid Id, string DisplayName, Guid? PictureId, string? Email, DateTimeOffset? RegisteredAt,
        DateTimeOffset? LastLoginAt, IReadOnlyList<string> Roles, BanInfo? Ban, ProfilePermissions Permissions,
        ModerationDetails? Moderation);

    [IgnoreETag]
    public static async Task<IResult> Get(
        Guid id, HttpContext http, UserProfiles profiles, UserManagementGate gate,
        IUserManagementQueries users, IAccessQueries access, IEmailQueries emails, ITwoFactorQueries twoFactor,
        TimeProvider timeProvider)
    {
        http.Response.Headers.CacheControl = "no-store";
        var profile = await profiles.GetPublic(id, http.RequestAborted);

        if (profile is null)
        {
            return Results.NotFound();
        }

        var canModerate = false;
        var canImpersonate = false;
        var canSendPrivateMessage = false;
        BanInfo? ban = null;
        ModerationDetails? moderation = null;
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var actor = await gate.FindActor(http);
            await actor.Match(
                () => Task.CompletedTask,
                async current =>
                {
                    canSendPrivateMessage = current.UserId != id
                        && id != DomainConstants.SystemUserId && id != DomainConstants.GuestUserId
                        && current.Access.Holds(GrantName.Of(GrantArea.PrivateMessage, GrantAction.Read))
                        && current.Access.Holds(GrantName.Of(GrantArea.PrivateMessage, GrantAction.Write));
                    var canManage = current.Access.Holds(GrantName.Of(GrantArea.User, GrantAction.Manage));
                    var canActAs = current.Access.Holds(GrantName.Of(GrantArea.Impersonation, GrantAction.Manage));
                    if (!canManage && !canActAs)
                    {
                        return;
                    }

                    var target = await users.TryFindUser(id, DeletionState.Active, timeProvider.GetUtcNow(), http.RequestAborted);
                    await target.Match(
                        () => Task.CompletedTask,
                        async user =>
                        {
                            canModerate = canManage && current.CanModerate(user);
                            var targetAccess = await access.GetUserAccess(id, http.RequestAborted);
                            if (canManage)
                            {
                                ban = user.Ban.Match<BanInfo?>(() => null,
                                    active => new BanInfo(active.Reason, active.Expires.Match<DateTimeOffset?>(() => null, value => value)));
                                var details = await profiles.GetManagementProfile(id, http.RequestAborted);
                                if (details is not null)
                                {
                                    var status = await twoFactor.GetStatus(id, http.RequestAborted);
                                    moderation = new ModerationDetails(details.DisplayName, details.Birthday, details.HideEmail,
                                        details.UpdatedAt,
                                        await emails.Addresses(id, http.RequestAborted),
                                        await profiles.GetBans(id, timeProvider.GetUtcNow(), http.RequestAborted),
                                        targetAccess.CalculatedGrants.Select(grant => grant.Name).ToList(),
                                        targetAccess.Grants.Select(grant => grant.Name).Order().ToList(),
                                        targetAccess.Restrictions.Select(restriction => restriction.Name).Order().ToList(),
                                        status.TotpEnabled, status.Passkeys.Count, status.RecoveryCodesRemaining);
                                }
                            }

                            if (canActAs && current.CanImpersonate(user))
                            {
                                canImpersonate = current.CanGrant(targetAccess.CalculatedGrants);
                            }
                        });
                });
        }

        return Results.Json(new ProfileResponse(profile.Id, profile.DisplayName, profile.PictureId,
            profile.PrimaryEmail, profile.RegisteredAt, profile.LastLoginAt, profile.Roles, ban,
            new ProfilePermissions(canModerate, canImpersonate, canSendPrivateMessage), moderation));
    }

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> GetDetails(
        HttpContext http, ISessionQueries sessions, UserProfiles profiles)
    {
        http.Response.Headers.CacheControl = "no-store";
        var current = await http.FindCurrentSession(sessions);
        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                var details = await profiles.GetDetails(session.UserId, http.RequestAborted);
                return details is null ? Results.Unauthorized() : Results.Json(details);
            });
    }

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> SetDetails(
        DetailsRequest details, HttpContext http, ISessionQueries sessions,
        UserProfiles profiles, TimeProvider timeProvider)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (details.HideEmail is null || details.Birthday > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
        {
            return Results.BadRequest();
        }

        var current = await http.FindCurrentSession(sessions);
        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session => await profiles.SetDetails(session.UserId,
                    new UserProfiles.Details(details.Birthday, details.HideEmail.Value), http.RequestAborted)
                ? Results.NoContent() : Results.Unauthorized());
    }
}
