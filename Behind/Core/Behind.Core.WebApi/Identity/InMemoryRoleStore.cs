using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;

namespace Behind.Core.WebApi.Identity;

public sealed class InMemoryRoleStore : IRoleStore<ApplicationRole>
{
    private readonly ConcurrentDictionary<string, ApplicationRole> _roles = new();

    public Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        _roles.TryAdd(role.Id, role);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        _roles.TryRemove(role.Id, out _);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<ApplicationRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        _roles.TryGetValue(roleId, out var role);
        return Task.FromResult(role);
    }

    public Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        var role = _roles.Values.FirstOrDefault(r => r.NormalizedName == normalizedRoleName);
        return Task.FromResult(role);
    }

    public Task<string> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.NormalizedName);

    public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Id);

    public Task<string> GetRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Name);

    public Task SetNormalizedRoleNameAsync(ApplicationRole role, string? normalizedName,
        CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task SetRoleNameAsync(ApplicationRole role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        _roles[role.Id] = role;
        return Task.FromResult(IdentityResult.Success);
    }

    public void Dispose()
    {
    }
}