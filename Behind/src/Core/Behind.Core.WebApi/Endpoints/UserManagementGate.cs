using Behind.Common;
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

                return Some(new UserManagementActor(session.UserId, roles.Any(HoldsDeveloperGrant)));
            });
    }

    public async Task<IResult> Run(
        HttpContext httpContext,
        Guid targetId,
        Func<UserManagementActor, IUserManagementQueries.ManagedUser, bool> isAllowed,
        Func<UserManagementActor, IUserManagementQueries.ManagedUser, DateTimeOffset, Task> action)
    {
        var actor = await FindActor(httpContext);

        return await actor.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async current =>
            {
                var now = timeProvider.GetUtcNow();
                var target = await userQueries.TryFindUser(targetId, now, httpContext.RequestAborted);

                return await target.Match(
                    () => Task.FromResult<IResult>(Results.NotFound()),
                    async user =>
                    {
                        if (!isAllowed(current, user))
                        {
                            return Results.StatusCode(StatusCodes.Status403Forbidden);
                        }

                        await action(current, user, now);

                        return Results.NoContent();
                    });
            });
    }

    private static bool HoldsDeveloperGrant(Role role) =>
        role.Grants.Match(
            () => false,
            grants => grants.Any(grant => grant.Name == ManagedRole.Developer.RequiredGrant));
}
