using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class UserManagementGate(
    ISessionQueries sessionQueries,
    IRoleQueries roleQueries,
    IUserManagementQueries userQueries,
    TimeProvider timeProvider)
{
    public async Task<Option<UserManagementActor>> FindActor(HttpContext httpContext)
    {
        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<Option<UserManagementActor>>(None),
            async session =>
            {
                var roles = await roleQueries.GetRolesByUserId(session.UserId, httpContext.RequestAborted);

                return Some(new UserManagementActor(
                    session.UserId,
                    roles.Any(role => role.Holds(ManagedRole.Developer.RequiredGrant)),
                    roles.Any(role => role.Holds(GrantName.Of(GrantArea.User, GrantAction.Delete)))));
            });
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
                    () => Task.FromResult<IResult>(Results.NotFound()),
                    async user =>
                    {
                        if (!isAllowed(current, user))
                        {
                            return Results.StatusCode(StatusCodes.Status403Forbidden);
                        }

                        return await action(current, user, now);
                    });
            });
    }

}
