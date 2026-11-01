using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public sealed class RoleManagementEndpoints
{
    public const int MaxNameLength = 100;

    public sealed record CreateRoleRequest([property: Required, MaxLength(MaxNameLength)] string Name);

    public sealed record AccessRequest(
        [property: Required] IReadOnlyList<string> Grants,
        [property: Required] IReadOnlyList<string> Restrictions);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Get(
        HttpContext httpContext,
        UserManagementGate gate,
        IRoleQueries roleQueries)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        return gate.RunAs(
            httpContext,
            async actor =>
            {
                var roles = await roleQueries.ListRoles(httpContext.RequestAborted);

                return Results.Json(new RoleListResponse(
                    roles.Select(role => role.ToItem(actor)).ToList(),
                    BuiltInAccess.GrantNames));
            });
    }

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Create(
        CreateRoleRequest request,
        HttpContext httpContext,
        UserManagementGate gate,
        IRoleCommands roleCommands) =>
        gate.RunAs(
            httpContext,
            async _ =>
            {
                var name = request.Name.Trim();

                if (name.Length == 0)
                {
                    return Results.BadRequest();
                }

                var created = await roleCommands.Create(name, httpContext.RequestAborted);

                return created.Match(() => Results.Conflict(), _ => Results.NoContent());
            });

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> SetAccess(
        Guid id,
        AccessRequest request,
        HttpContext httpContext,
        UserManagementGate gate,
        IRoleQueries roleQueries,
        IAccessCommands accessCommands) =>
        WithRole(
            id,
            httpContext,
            gate,
            roleQueries,
            async (actor, found) =>
            {
                var selection = new AccessSelection(request.Grants, request.Restrictions).Normalized;

                if (!selection.IsValid)
                {
                    return Results.BadRequest();
                }

                if (!actor.CanChangeRole(found.Role, selection))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                await accessCommands.SetRoleAccess(id, selection, httpContext.RequestAborted);

                return Results.NoContent();
            });

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Delete(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IRoleQueries roleQueries,
        IRoleCommands roleCommands) =>
        WithRole(
            id,
            httpContext,
            gate,
            roleQueries,
            async (actor, found) =>
            {
                if (!actor.CanDeleteRole(id, found.Role))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                await roleCommands.Delete(id, httpContext.RequestAborted);

                return Results.NoContent();
            });

    private static Task<IResult> WithRole(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        IRoleQueries roleQueries,
        Func<UserManagementActor, IRoleQueries.RoleResult, Task<IResult>> action) =>
        gate.RunAs(
            httpContext,
            async actor =>
            {
                var found = await roleQueries.GetRoleById(id, httpContext.RequestAborted);

                return await found.Match(
                    () => Task.FromResult<IResult>(Results.NotFound()),
                    role => action(actor, role));
            });
}
