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
    public sealed record ProfilePermissions(bool CanModerate, bool CanImpersonate);

    public sealed record DetailsRequest(DateOnly? Birthday, bool? HideEmail);

    public sealed record ProfileResponse(
        Guid Id, string DisplayName, Guid? PictureId, string? Email, BanInfo? Ban, ProfilePermissions Permissions);

    [IgnoreETag]
    public static async Task<IResult> Get(
        Guid id, HttpContext http, UserProfiles profiles, UserManagementGate gate,
        IUserManagementQueries users, IAccessQueries access, TimeProvider timeProvider)
    {
        http.Response.Headers.CacheControl = "no-store";
        var profile = await profiles.GetPublic(id, http.RequestAborted);

        if (profile is null)
        {
            return Results.NotFound();
        }

        var canModerate = false;
        var canImpersonate = false;
        BanInfo? ban = null;
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var actor = await gate.FindActor(http);
            await actor.Match(
                () => Task.CompletedTask,
                async current =>
                {
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
                            if (canManage)
                            {
                                ban = user.Ban.Match<BanInfo?>(() => null,
                                    active => new BanInfo(active.Reason, active.Expires.Match<DateTimeOffset?>(() => null, value => value)));
                            }

                            if (canActAs && current.CanImpersonate(user))
                            {
                                var targetAccess = await access.GetUserAccess(id, http.RequestAborted);
                                canImpersonate = current.CanGrant(targetAccess.CalculatedGrants);
                            }
                        });
                });
        }

        return Results.Json(new ProfileResponse(profile.Id, profile.DisplayName, profile.PictureId,
            profile.PrimaryEmail, ban, new ProfilePermissions(canModerate, canImpersonate)));
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
