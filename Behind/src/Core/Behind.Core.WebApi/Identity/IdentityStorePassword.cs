using Behind.Core.Domain;
using Microsoft.AspNetCore.Identity;

namespace Behind.Core.WebApi.Identity;

public partial class IdentityStore : IUserPasswordStore<ApplicationUser>
{
    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            passwordCommands.InactivateFromUserId(user.Id, cancellationToken);
            passwordCommands.CreateFromUserId(user.Id, new Password(passwordHash, true), cancellationToken);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Password?.ToString());

    public async Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var passwords = await passwordQueries.GetPasswordsFromUserId(user.Id, cancellationToken);

        return passwords.Any(x => x.IsActive);
    }
}