using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class AccessEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;

    public sealed record SaveAccessSettingsRequest(
        bool RestrictionEnabled,
        [property: Required, MaxLength(AccessSettings.MaxRoleLength)] string RestrictedToRole,
        [property: MaxLength(AccessSettings.MaxMessageLength)] string? RestrictionMessage,
        bool ModeratedSignUp,
        [property: MaxLength(AccessSettings.MaxTitleLength)] string? RestrictionTitle = null);

    [IgnoreETag]
    public static async Task<IResult> Restriction(HttpContext httpContext, AccessGate gate)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        return Results.Json((await gate.Decide(httpContext)).ToResponse());
    }

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        IAccessSettingsQueries queries,
        IRoleQueries roles,
        IAuthorizationService authorization,
        [FromQuery] bool deleted = false) =>
        httpContext.WithDeletionState(
            authorization,
            deleted,
            async state =>
            {
                var settings = state == DeletionState.Deleted
                    ? await queries.FindDeleted(httpContext.RequestAborted)
                    : Some(await queries.Current(httpContext.RequestAborted));

                var names = (await roles.ListRoles(httpContext.RequestAborted))
                    .Select(role => role.Role.Name)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return settings.Match(
                    () => Results.NotFound(),
                    found => Results.Json(new AccessManageResponse(
                        found.ToResponse(state == DeletionState.Deleted),
                        names)));
            });

    [IgnoreETag]
    public static async Task<IResult> Save(
        SaveAccessSettingsRequest request,
        HttpContext httpContext,
        IAccessSettingsCommands commands) =>
        (await commands.Save(request.ToDraft(), httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Delete(HttpContext httpContext, IAccessSettingsCommands commands) =>
        await commands.Delete(httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> Restore(HttpContext httpContext, IAccessSettingsCommands commands)
    {
        var result = await commands.Restore(httpContext.RequestAborted);

        return result is AccessSettingsSaveResult.Saved ? Results.NoContent() : result.ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> HardDelete(HttpContext httpContext, IAccessSettingsCommands commands) =>
        (await commands.HardDelete(httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> PendingUsers(
        HttpContext httpContext,
        IUserApprovalQueries queries,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var users = await queries.ListPending(
            Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
            search.ToSearchTerm(ManagedListExtensions.MaxSearchLength),
            httpContext.RequestAborted);

        return Results.Json(new PendingUsersResponse(users.ToPageResponse(user => user.ToResponse())));
    }

    [IgnoreETag]
    public static Task<IResult> Approve(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IUserApprovalCommands approvals) =>
        gate.RunForResult(
            httpContext,
            id,
            (actor, target) => actor.CanModerate(target),
            async (_, target, _) => await approvals.Approve(target.Id, httpContext.RequestAborted)
                ? Results.NoContent()
                : Results.NotFound());

    [IgnoreETag]
    public static Task<IResult> Reject(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IUserApprovalQueries approvals,
        IUserCommands users) =>
        gate.RunForResult(
            httpContext,
            id,
            (actor, target) => actor.CanDelete(target),
            async (_, target, _) =>
            {
                if (!await approvals.IsPending(target.Id, httpContext.RequestAborted))
                {
                    return Results.NotFound();
                }

                await users.Delete(target.Id, httpContext.RequestAborted);

                return Results.NoContent();
            });
}
