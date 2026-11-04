using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class UserManagementEndpoints
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;
    public const int MaxReasonLength = 500;

    public sealed record BanRequest([property: Required, MaxLength(MaxReasonLength)] string Reason);

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Get(
        HttpContext httpContext,
        UserManagementGate gate,
        IUserManagementQueries userQueries,
        TimeProvider timeProvider,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? letter = null,
        [FromQuery] bool deleted = false)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        var letterFilter = LetterBucketExtensions.ParseLetterBucket(letter);

        if (!string.IsNullOrWhiteSpace(letter) && letterFilter == None)
        {
            return Results.BadRequest();
        }

        var actor = await gate.FindActor(httpContext);
        var state = deleted ? DeletionState.Deleted : DeletionState.Active;

        return await actor.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async current =>
            {
                if (deleted && !current.IsDeveloper)
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                var searchTerm = search.ToSearchTerm(MaxSearchLength);

                var users = await userQueries.ListUsers(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    searchTerm,
                    letterFilter,
                    state,
                    timeProvider.GetUtcNow(),
                    httpContext.RequestAborted);

                var letters = await userQueries.GetLetters(searchTerm, state, httpContext.RequestAborted);

                return Results.Json(new UserListResponse(
                    users.ToPageResponse(user => user.ToItem(current)),
                    letters));
            });
    }

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Elevate(
        Guid id,
        string role,
        HttpContext httpContext,
        UserManagementGate gate,
        IRoleCommands roleCommands) =>
        ChangeRole(id, role, httpContext, gate, (managed, user) => roleCommands.AssignBuiltIn(
            user.Id,
            managed.Role,
            httpContext.RequestAborted));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Deelevate(
        Guid id,
        string role,
        HttpContext httpContext,
        UserManagementGate gate,
        IRoleCommands roleCommands) =>
        ChangeRole(id, role, httpContext, gate, (managed, user) => roleCommands.RevokeBuiltIn(
            user.Id,
            managed.Role,
            httpContext.RequestAborted));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> SignOut(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        ISessionCommands sessionCommands) =>
        gate.Run(
            httpContext,
            id,
            (actor, target) => actor.CanModerate(target),
            (actor, target, now) => sessionCommands.InvalidateAllSessions(
                target.Id,
                actor.UserId,
                now,
                httpContext.RequestAborted));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Ban(
        Guid id,
        BanRequest request,
        HttpContext httpContext,
        UserManagementGate gate,
        IWallOfShameCommands wallOfShameCommands,
        ISessionCommands sessionCommands) =>
        gate.Run(
            httpContext,
            id,
            (actor, target) => actor.CanModerate(target),
            async (actor, target, now) =>
            {
                await wallOfShameCommands.Ban(
                    target.Id,
                    actor.UserId,
                    new Reason(request.Reason.Trim()),
                    httpContext.RequestAborted);

                await sessionCommands.InvalidateAllSessions(
                    target.Id,
                    actor.UserId,
                    now,
                    httpContext.RequestAborted);
            });

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Unban(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IWallOfShameCommands wallOfShameCommands) =>
        gate.Run(
            httpContext,
            id,
            (actor, target) => actor.CanModerate(target),
            (_, target, _) => wallOfShameCommands.Unban(target.Id, httpContext.RequestAborted));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> ConfirmEmail(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IEmailCommands emailCommands) =>
        gate.Run(
            httpContext,
            id,
            (actor, target) => actor.CanConfirmEmail(target),
            (_, target, _) => emailCommands.ConfirmPrimary(target.Id, httpContext.RequestAborted));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Delete(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IUserCommands userCommands) =>
        gate.Run(
            httpContext,
            id,
            (actor, target) => actor.CanDelete(target),
            (_, target, _) => userCommands.Delete(target.Id, httpContext.RequestAborted));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Restore(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IUserCommands userCommands) =>
        gate.Run(
            httpContext,
            id,
            (actor, target) => actor.CanManageDeleted(target),
            (_, target, _) => userCommands.Restore(target.Id, httpContext.RequestAborted),
            DeletionState.Deleted);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> HardDelete(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IUserCommands userCommands) =>
        gate.RunForResult(
            httpContext,
            id,
            (actor, target) => actor.CanManageDeleted(target),
            async (_, target, _) => await userCommands.HardDelete(target.Id, httpContext.RequestAborted)
                ? Results.NoContent()
                : Results.Conflict(),
            DeletionState.Deleted);

    private static Task<IResult> ChangeRole(
        Guid id,
        string role,
        HttpContext httpContext,
        UserManagementGate gate,
        Func<ManagedRole, IUserManagementQueries.ManagedUser, Task> change) =>
        ManagedRole.Parse(role).Match(
            () => Task.FromResult<IResult>(Results.NotFound()),
            managed => gate.Run(
                httpContext,
                id,
                (actor, target) => actor.CanChange(managed, target),
                (_, target, _) => change(managed, target)));
}
