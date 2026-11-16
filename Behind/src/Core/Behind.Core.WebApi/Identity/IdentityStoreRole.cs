using Behind.Common.Logging;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Identity;

public partial class IdentityStore : IRoleStore<ApplicationRole>
{
    public async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        try
        {
            var repository = unitOfWork.GetRepository<UserRole, Guid>();
            var exists = await repository.TryFindAsync(role.Id, cancellationToken);

            return await exists.Match(
                none: async () =>
                {
                    await roleCommands.CreateWithId(role.Id, new UserRole(role.Name), cancellationToken);

                    return IdentityResult.Success;
                },
                some: _ =>
                {
                    logger.LogWarning("Role {RoleId} was not created: it already exists", () => [role.Id]);

                    return Task.FromResult(IdentityResult.Failed(new IdentityError
                    {
                        Code = "RoleAlreadyExists",
                        Description = "Role already exists"
                    }));
                }
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to create role {RoleId}", () => [role.Id]);

            return IdentityResult.Failed();
        }
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        try
        {
            var repository = unitOfWork.GetRepository<UserRole, Guid>();
            var dbRole = await repository.TryFindAsync(role.Id, cancellationToken);

            return await dbRole.Match(
                none: () =>
                {
                    logger.LogWarning("Role {RoleId} was not updated: it does not exist", () => [role.Id]);

                    return Task.FromResult(IdentityResult.Failed());
                },
                some: async r =>
                {
                    repository.Update(role.Id, r with { Name = role.Name });
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return IdentityResult.Success;
                }
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to update role {RoleId}", () => [role.Id]);

            return IdentityResult.Failed();
        }
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.GetRepository<UserRole, Guid>().DeleteAsync(role.Id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to delete role {RoleId}", () => [role.Id]);

            return IdentityResult.Failed();
        }
    }

    public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Id.ToString());

    public Task<string?> GetRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(role.Name);

    public Task SetRoleNameAsync(ApplicationRole role, string? roleName, CancellationToken cancellationToken)
    {
        if (roleName is not null)
        {
            role.Name = roleName;
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(role.Name.ToUpperInvariant());

    public Task SetNormalizedRoleNameAsync(ApplicationRole role, string? normalizedName,
        CancellationToken cancellationToken) => Task.CompletedTask;

    async Task<ApplicationRole?> IRoleStore<ApplicationRole>.FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(roleId, out var id))
            {
                logger.LogDebug("Role lookup ignored: {RoleId} is not a valid identifier", () => [roleId]);

                return null;
            }

            return (await unitOfWork.GetRepository<UserRole, Guid>().TryFindAsync(id, cancellationToken))
                .Match(
                    none: () => null!,
                    some: role => role.ToApplicationRole(id)
                );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to find role {RoleId}", () => [roleId]);

            return null;
        }
    }

    async Task<ApplicationRole?> IRoleStore<ApplicationRole>.FindByNameAsync(string normalizedRoleName,
        CancellationToken cancellationToken)
    {
        return (await roleQueries.GetRoleByName(normalizedRoleName, cancellationToken))
            .Match(
                none: () => null!,
                some: role => role.Role.ToApplicationRole(role.Id)
            );
    }
}