using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Logging;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class UserManagementGate(
    ISessionQueries sessionQueries,
    IAccessQueries accessQueries,
    IUserManagementQueries userQueries,
    AccessCatalog catalog,
    TimeProvider timeProvider,
    ILogger<UserManagementGate> logger)
{
    public async Task<Option<UserManagementActor>> FindActor(HttpContext httpContext)
    {
        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () =>
            {
                logger.LogWarning(
                    "Rejected {Method} {Path}: no active session",
                    () => [httpContext.Request.Method, httpContext.Request.Path.Value]);

                return Task.FromResult<Option<UserManagementActor>>(None);
            },
            async session =>
            {
                var access = await accessQueries.GetUserAccess(session.UserId, httpContext.RequestAborted);

                logger.LogDebug(
                    "Resolved actor {ActorId} (developer: {IsDeveloper}) for {Method} {Path}",
                    () => [session.UserId, access.IsDeveloper, httpContext.Request.Method, httpContext.Request.Path.Value]);

                return Some(new UserManagementActor(
                    session.UserId,
                    access.IsDeveloper,
                    access.Holds(GrantName.Of(GrantArea.User, GrantAction.Delete)),
                    access,
                    catalog,
                    session.IsImpersonated));
            });
    }

    public async Task<IResult> RunAs(
        HttpContext httpContext,
        Func<UserManagementActor, Task<IResult>> action)
    {
        var actor = await FindActor(httpContext);

        return await actor.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async current => LogOutcome(httpContext, current, null, await action(current)));
    }

    public Task<IResult> Run(
        HttpContext httpContext,
        Guid targetId,
        Func<UserManagementActor, IUserManagementQueries.ManagedUser, bool> isAllowed,
        Func<UserManagementActor, IUserManagementQueries.ManagedUser, DateTimeOffset, Task> action,
        DeletionState state = DeletionState.Active) =>
        RunForResult(
            httpContext,
            targetId,
            isAllowed,
            async (actor, target, now) =>
            {
                await action(actor, target, now);

                return Results.NoContent();
            },
            state);

    public async Task<IResult> RunForResult(
        HttpContext httpContext,
        Guid targetId,
        Func<UserManagementActor, IUserManagementQueries.ManagedUser, bool> isAllowed,
        Func<UserManagementActor, IUserManagementQueries.ManagedUser, DateTimeOffset, Task<IResult>> action,
        DeletionState state = DeletionState.Active)
    {
        var actor = await FindActor(httpContext);

        return await actor.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async current =>
            {
                var now = timeProvider.GetUtcNow();
                var target = await userQueries.TryFindUser(targetId, state, now, httpContext.RequestAborted);

                return await target.Match(
                    () =>
                    {
                        logger.LogWarning(
                            "Actor {ActorId} requested {Method} {Path} for unknown {State} user {TargetId}",
                            () => [current.UserId, httpContext.Request.Method, httpContext.Request.Path.Value, state, targetId]);

                        return Task.FromResult<IResult>(Results.NotFound());
                    },
                    async user =>
                    {
                        if (!isAllowed(current, user))
                        {
                            logger.LogWarning(
                                "Actor {ActorId} is not allowed to {Method} {Path} on user {TargetId}",
                                () => [current.UserId, httpContext.Request.Method, httpContext.Request.Path.Value, user.Id]);

                            return Results.StatusCode(StatusCodes.Status403Forbidden);
                        }

                        return LogOutcome(httpContext, current, user.Id, await action(current, user, now));
                    });
            });
    }

    private IResult LogOutcome(
        HttpContext httpContext,
        UserManagementActor actor,
        Guid? targetId,
        IResult result)
    {
        var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;

        logger.Log(
            LevelFor(httpContext.Request.Method, statusCode),
            "Actor {ActorId} completed {Method} {Path} on target {TargetId} with {StatusCode}",
            () => [actor.UserId, httpContext.Request.Method, httpContext.Request.Path.Value, targetId, statusCode]);

        return result;
    }

    private static LogLevel LevelFor(string method, int statusCode) =>
        statusCode >= StatusCodes.Status400BadRequest ? LogLevel.Warning
        : HttpMethods.IsGet(method) ? LogLevel.Debug
        : LogLevel.Information;
}
