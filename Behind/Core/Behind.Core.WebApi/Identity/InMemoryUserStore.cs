using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Behind.Core.WebApi.Identity;

public sealed class InMemoryUserStore : IUserEmailStore<ApplicationUser>, IUserPasswordStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>
{
    private readonly ConcurrentDictionary<string, ApplicationUser> _users = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _userRoles = new();

    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        _users.TryAdd(user.Id, user);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        _users.TryRemove(user.Id, out _);
        _userRoles.TryRemove(user.Id, out _);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        _users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        var user = _users.Values.FirstOrDefault(u => u.NormalizedUserName == normalizedUserName);
        return Task.FromResult(user);
    }

    public Task<string> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.NormalizedUserName);

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id);

    public Task<string> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.UserName);

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName,
        CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        _users[user.Id] = user;
        return Task.FromResult(IdentityResult.Success);
    }

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        if (passwordHash != null)
        {
            user.Password = passwordHash;
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult<string?>(user.Password ?? string.Empty);

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.Password != null);

    public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        var roles = _userRoles.GetOrAdd(user.Id, _ => new HashSet<string>());
        lock (roles)
        {
            roles.Add(roleName);
        }

        return Task.CompletedTask;
    }

    public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        if (_userRoles.TryGetValue(user.Id, out var roles))
        {
            lock (roles)
            {
                roles.Remove(roleName);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (_userRoles.TryGetValue(user.Id, out var roles))
        {
            lock (roles)
            {
                return Task.FromResult<IList<string>>(roles.ToList());
            }
        }

        return Task.FromResult<IList<string>>(new List<string>());
    }

    public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        if (_userRoles.TryGetValue(user.Id, out var roles))
        {
            lock (roles)
            {
                return Task.FromResult(roles.Contains(roleName));
            }
        }

        return Task.FromResult(false);
    }

    public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var userIdsInRole = _userRoles
            .Where(kvp => kvp.Value.Contains(roleName))
            .Select(kvp => kvp.Key)
            .ToHashSet();

        var usersInRole = _users.Values
            .Where(u => userIdsInRole.Contains(u.Id))
            .ToList();

        return Task.FromResult<IList<ApplicationUser>>(usersInRole);
    }

    public void Dispose()
    {
    }

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.UserName = email ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.UserName);

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.SingleOrDefault(u =>
            u.NormalizedUserName.Equals(normalizedEmail, StringComparison.InvariantCultureIgnoreCase)));

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.NormalizedUserName);

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedEmail ?? string.Empty;
        return Task.CompletedTask;
    }
}