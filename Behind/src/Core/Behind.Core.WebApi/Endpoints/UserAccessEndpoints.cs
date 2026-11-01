using System.ComponentModel.DataAnnotations;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public sealed class UserAccessEndpoints
{
    public sealed record UserAccessRequest(
        [property: Required] IReadOnlyList<Guid> RoleIds,
        [property: Required] IReadOnlyList<string> Grants,
        [property: Required] IReadOnlyList<string> Restrictions);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Get(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IAccessQueries accessQueries,
        IRoleQueries roleQueries)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        return gate.RunForResult(
            httpContext,
            id,
            (_, _) => true,
            async (actor, target, _) =>
            {
                var access = await accessQueries.GetUserAccess(target.Id, httpContext.RequestAborted);
                var roles = await roleQueries.ListRoles(httpContext.RequestAborted);

                return Results.Json(access.ToResponse(target.Id, actor.CanModerate(target), roles));
            });
    }

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Set(
        Guid id,
        UserAccessRequest request,
        HttpContext httpContext,
        UserManagementGate gate,
        IAccessQueries accessQueries,
        IRoleQueries roleQueries,
        IAccessCommands accessCommands) =>
        gate.RunForResult(
            httpContext,
            id,
            (actor, target) => actor.CanModerate(target),
            async (actor, target, _) =>
            {
                var selection = new AccessSelection(request.Grants, request.Restrictions).Normalized;
                var roleIds = request.RoleIds.Distinct().ToList();
                var roles = await roleQueries.ListRoles(httpContext.RequestAborted);
                var chosen = roles.Where(role => roleIds.Contains(role.Id)).ToList();

                if (!selection.IsValid
                    || chosen.Count != roleIds.Count
                    || !roleIds.Contains(BuiltInAccess.Users.Id))
                {
                    return Results.BadRequest();
                }

                var current = await accessQueries.GetUserAccess(target.Id, httpContext.RequestAborted);
                var proposed = current.With(chosen.Select(role => role.Role).ToList(), selection);

                if (!actor.CanChangeAccess(current, proposed))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                await accessCommands.SetUserAccess(target.Id, roleIds, selection, httpContext.RequestAborted);

                return Results.NoContent();
            });
}
