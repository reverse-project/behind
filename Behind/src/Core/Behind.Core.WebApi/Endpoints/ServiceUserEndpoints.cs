using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

/// <summary>A named service identity with its own independently managed access.</summary>
public sealed record ServiceUserResponse(Guid Id, long Version, string Name, bool Enabled);

/// <summary>A service identity and its new bearer credential, revealed only once.</summary>
public sealed record ServiceUserCredentialResponse(ServiceUserResponse User, string ApiKey);

/// <summary>Service identities and the current administrator's permitted management actions.</summary>
public sealed record ServiceUserListResponse(
    IReadOnlyList<ServiceUserResponse> Users, bool CanManage, bool CanDelete, bool IsDeveloper);

/// <summary>Creates a named service identity independent of the creator.</summary>
public sealed record CreateServiceUserRequest([property: JsonRequired] string Name);

/// <summary>Changes a service identity's enabled state at the observed version.</summary>
public sealed record UpdateServiceUserRequest([property: JsonRequired] long Version, [property: JsonRequired] bool Enabled);

/// <summary>The observed service identity version for a destructive or credential operation.</summary>
public sealed record ServiceUserVersionRequest([property: JsonRequired] long Version);

public sealed record ServiceUserAccessRequest(
    [property: JsonRequired] long Version,
    [property: JsonRequired] IReadOnlyList<Guid> RoleIds,
    [property: JsonRequired] IReadOnlyList<string> Grants,
    [property: JsonRequired] IReadOnlyList<string> Restrictions);

public static class ServiceUserEndpoints
{
    public static async Task<IResult> Get(
        Guid id, HttpContext context, IServiceUserStore store, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await store.Find(id, cancellationToken)).Match<IResult>(
            () => TypedResults.NotFound(), entry => TypedResults.Ok(ToResponse(entry)));
    }

    public static Task<IResult> List(
        HttpContext context, UserManagementGate gate, IServiceUserStore store,
        IOptions<SecurityConfiguration> security, CancellationToken cancellationToken, bool deleted = false) =>
        gate.RunAs(context, async actor =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (deleted && !actor.IsDeveloper)
            {
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
            }
            var users = await store.List(deleted, cancellationToken);
            return TypedResults.Ok(new ServiceUserListResponse(
                users.Select(ToResponse).ToList(),
                !security.Value.ReadOnlyConfiguration && !actor.IsImpersonating
                    && actor.Access.Holds(GrantName.Of(GrantArea.Configuration, GrantAction.Manage))
                    && actor.Access.Holds(GrantName.Of(GrantArea.User, GrantAction.Manage)),
                !security.Value.ReadOnlyConfiguration && !actor.IsImpersonating
                    && actor.Access.Holds(GrantName.Of(GrantArea.Configuration, GrantAction.Manage))
                    && actor.CanDeleteUsers,
                actor.IsDeveloper && !actor.IsImpersonating));
        });

    public static Task<IResult> Create(
        CreateServiceUserRequest request, HttpContext context, UserManagementGate gate, IServiceUserStore store,
        IOptions<SecurityConfiguration> security, CancellationToken cancellationToken) =>
        gate.RunAs(context, async actor =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!CanModify(actor, security))
            {
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
            }
            if (!ServiceUser.IsValidName(request.Name))
            {
                return TypedResults.BadRequest();
            }
            var created = await store.Create(request.Name.Trim(), cancellationToken);
            return created.Match<IResult>(
                () => TypedResults.BadRequest(),
                credential => TypedResults.Created($"/service-users/{credential.User.Id}",
                    new ServiceUserCredentialResponse(ToResponse(credential.User), credential.ApiKey)));
        });

    public static Task<IResult> Update(
        Guid id, UpdateServiceUserRequest request, HttpContext context, UserManagementGate gate,
        IServiceUserStore store, IAccessQueries access, IOptions<SecurityConfiguration> security,
        CancellationToken cancellationToken) =>
        request.Version <= 0 ? Task.FromResult<IResult>(TypedResults.BadRequest()) :
        Modify(id, context, gate, store, access, security, cancellationToken, async _ =>
            await store.SetEnabled(id, request.Version, request.Enabled, cancellationToken)
                ? TypedResults.NoContent() : TypedResults.Conflict());

    public static Task<IResult> Rotate(
        Guid id, ServiceUserVersionRequest request, HttpContext context, UserManagementGate gate,
        IServiceUserStore store, IAccessQueries access, IOptions<SecurityConfiguration> security,
        CancellationToken cancellationToken) =>
        request.Version <= 0 ? Task.FromResult<IResult>(TypedResults.BadRequest()) :
        Modify(id, context, gate, store, access, security, cancellationToken, async _ =>
        {
            var rotated = await store.Rotate(id, request.Version, cancellationToken);
            return rotated.Match<IResult>(
                () => TypedResults.Conflict(),
                credential => TypedResults.Ok(new ServiceUserCredentialResponse(ToResponse(credential.User), credential.ApiKey)));
        });

    public static Task<IResult> Delete(
        Guid id, ServiceUserVersionRequest request, HttpContext context, UserManagementGate gate,
        IServiceUserStore store, IOptions<SecurityConfiguration> security, CancellationToken cancellationToken) =>
        gate.RunAs(context, async actor =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (security.Value.ReadOnlyConfiguration || actor.IsImpersonating || !actor.CanDeleteUsers)
            {
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
            }
            if (request.Version <= 0)
            {
                return TypedResults.BadRequest();
            }
            return await store.Delete(id, request.Version, cancellationToken)
                ? TypedResults.NoContent() : TypedResults.Conflict();
        });

    public static Task<IResult> Restore(
        Guid id, ServiceUserVersionRequest request, HttpContext context, UserManagementGate gate, IServiceUserStore store,
        IOptions<SecurityConfiguration> security, CancellationToken cancellationToken) =>
        gate.RunAs(context, async actor =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (security.Value.ReadOnlyConfiguration || actor.IsImpersonating || !actor.IsDeveloper)
            {
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
            }
            if (request.Version <= 0)
            {
                return TypedResults.BadRequest();
            }
            var deleted = await store.List(true, cancellationToken);
            var user = deleted.SingleOrDefault(entry => entry.Id == id);
            if (user is null)
            {
                return TypedResults.NotFound();
            }
            if (user.Version != request.Version)
            {
                return TypedResults.Conflict();
            }
            return await store.Restore(id, request.Version, cancellationToken)
                ? TypedResults.NoContent() : TypedResults.Conflict();
        });

    public static Task<IResult> HardDelete(
        Guid id, ServiceUserVersionRequest request, HttpContext context, UserManagementGate gate, IServiceUserStore store,
        IOptions<SecurityConfiguration> security, CancellationToken cancellationToken) =>
        gate.RunAs(context, async actor =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (security.Value.ReadOnlyConfiguration || actor.IsImpersonating || !actor.IsDeveloper)
            {
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
            }
            if (request.Version <= 0)
            {
                return TypedResults.BadRequest();
            }
            var deleted = await store.List(true, cancellationToken);
            var user = deleted.SingleOrDefault(entry => entry.Id == id);
            if (user is null)
            {
                return TypedResults.NotFound();
            }
            if (user.Version != request.Version)
            {
                return TypedResults.Conflict();
            }
            return await store.HardDelete(id, request.Version, cancellationToken)
                ? TypedResults.NoContent() : TypedResults.Conflict();
        });

    private static Task<IResult> Modify(
        Guid id, HttpContext context, UserManagementGate gate, IServiceUserStore store, IAccessQueries access,
        IOptions<SecurityConfiguration> security, CancellationToken cancellationToken,
        Func<UserManagementActor, Task<IResult>> action) =>
        gate.RunAs(context, async actor =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!CanModify(actor, security))
            {
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
            }
            var user = await store.Find(id, cancellationToken);
            return await user.Match(
                () => Task.FromResult<IResult>(TypedResults.NotFound()),
                async found => actor.CanGrant((await access.GetUserAccess(found.Id, cancellationToken)).CalculatedGrants)
                    ? await action(actor) : TypedResults.StatusCode(StatusCodes.Status403Forbidden));
        });

    private static bool CanModify(UserManagementActor actor, IOptions<SecurityConfiguration> security) =>
        !security.Value.ReadOnlyConfiguration && !actor.IsImpersonating
        && actor.Access.Holds(GrantName.Of(GrantArea.Configuration, GrantAction.Manage))
        && actor.Access.Holds(GrantName.Of(GrantArea.User, GrantAction.Manage));

    public static Task<IResult> GetAccess(
        Guid id, HttpContext context, UserManagementGate gate, IServiceUserStore store,
        IAccessQueries access, IRoleQueries roles, CancellationToken cancellationToken) =>
        gate.RunAs(context, async actor =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var found = await store.Find(id, cancellationToken);
            if (found == None)
            {
                return TypedResults.NotFound();
            }
            var current = await access.GetUserAccess(id, cancellationToken);
            var canEdit = !actor.IsImpersonating
                && actor.Access.Holds(GrantName.Of(GrantArea.Configuration, GrantAction.Manage))
                && actor.Access.Holds(GrantName.Of(GrantArea.User, GrantAction.Manage))
                && actor.CanGrant(current.CalculatedGrants);
            var version = found.Match(() => 0L, entry => entry.Version);
            return TypedResults.Ok(current.ToResponse(id, canEdit, await roles.ListRoles(cancellationToken), actor.Catalog, version));
        });

    public static Task<IResult> SetAccess(
        Guid id, ServiceUserAccessRequest request, HttpContext context, UserManagementGate gate,
        IServiceUserStore store, IAccessQueries access, IRoleQueries roles, IAccessCommands commands,
        IOptions<SecurityConfiguration> security, CancellationToken cancellationToken) =>
        Modify(id, context, gate, store, access, security, cancellationToken, async actor =>
        {
            if (request.Version <= 0)
            {
                return TypedResults.BadRequest();
            }
            if (request.RoleIds is null || request.Grants is null || request.Restrictions is null)
            {
                return TypedResults.BadRequest();
            }
            var selection = new AccessSelection(request.Grants, request.Restrictions).Normalized;
            var roleIds = request.RoleIds.Distinct().ToList();
            var allRoles = await roles.ListRoles(cancellationToken);
            var chosen = allRoles.Where(role => roleIds.Contains(role.Id)).ToList();
            if (!actor.Catalog.IsValid(selection) || chosen.Count != roleIds.Count
                || !roleIds.Contains(BuiltInAccess.Users.Id))
            {
                return TypedResults.BadRequest();
            }
            var current = await access.GetUserAccess(id, cancellationToken);
            if (!actor.CanChangeAccess(current, current.With(chosen.Select(role => role.Role).ToList(), selection)))
            {
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
            }
            if (!await store.SetAccess(id, request.Version, roleIds, selection, cancellationToken))
            {
                return TypedResults.Conflict();
            }
            return TypedResults.NoContent();
        });

    private static ServiceUserResponse ToResponse(ServiceUserEntry entry) =>
        new(entry.Id, entry.Version, entry.User.Name, entry.User.Enabled);
}
