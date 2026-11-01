using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class RoleManagementEndpoints
{
    public const int MaxNameLength = 100;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;
    public const string DeletedRecordsGrant = "RawDatabase.Manage";

    public sealed record CreateRoleRequest([property: Required, MaxLength(MaxNameLength)] string Name);

    public sealed record AccessRequest(
        [property: Required] IReadOnlyList<string> Grants,
        [property: Required] IReadOnlyList<string> Restrictions);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Get(
        HttpContext httpContext,
        UserManagementGate gate,
        IRoleQueries roleQueries,
        IAuthorizationService authorization,
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
            return Task.FromResult(Results.BadRequest());
        }

        return gate.RunAs(
            httpContext,
            async actor =>
            {
                if (deleted && !await authorization.HoldsGrantAsync(httpContext.User, DeletedRecordsGrant))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                var state = deleted ? DeletionState.Deleted : DeletionState.Active;
                var searchTerm = search.ToSearchTerm(MaxSearchLength);

                var roles = await roleQueries.PageRoles(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    searchTerm,
                    letterFilter,
                    state,
                    httpContext.RequestAborted);

                var letters = await roleQueries.GetLetters(searchTerm, state, httpContext.RequestAborted);

                return Results.Json(new RoleListResponse(
                    roles.ToPageResponse(role => role.ToItem(actor, state)),
                    BuiltInAccess.GrantNames,
                    letters));
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

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IRoleCommands roleCommands) =>
        await roleCommands.Restore(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, IRoleCommands roleCommands) =>
        (await roleCommands.HardDelete(id, httpContext.RequestAborted)).ToResult();

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
