using Behind.Core.Domain;
using Microsoft.AspNetCore.Identity;

namespace Behind.Core.WebApi.Identity;

public partial class IdentityStore : IUserPasswordStore<ApplicationUser>
{
    public async Task SetPasswordHashAsync(
        ApplicationUser user,
        string? passwordHash,
        CancellationToken cancellationToken
    )
    {
        await passwordCommands.InactivateFromUserId(user.Id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            await passwordCommands.CreateFromUserId(user.Id, new Password(passwordHash, true), cancellationToken);
        }
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Password?.ToString());

    public async Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var passwords = await passwordQueries.GetPasswordsFromUserId(user.Id, cancellationToken);

        return passwords.Any(x => x.IsActive);
    }
}