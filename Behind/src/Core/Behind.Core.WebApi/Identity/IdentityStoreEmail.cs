using static Behind.Common.FunctionalExtensions;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Identity;

namespace Behind.Core.WebApi.Identity;

public partial class IdentityStore : IUserEmailStore<ApplicationUser>
{
    public void Dispose() => Unit();

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Emails.FirstOrDefault()?.Value);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Emails.FirstOrDefault()?.Value.ToUpperInvariant());

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await emailQueries.GetEmailByValue(user.Emails.First().Value, cancellationToken);

            return await exists.Match<Task<IdentityResult>>(
                none: async () =>
                {
                    await userCommands.CreateWithId(
                        user.Id,
                        new HumanUser(user.DisplayName, user.Emails, None),
                        cancellationToken
                    );
                    return IdentityResult.Success;
                },
                some: _ => Task.FromResult(IdentityResult.Failed(new IdentityError
                {
                    Code = "EmailAlreadyExists",
                    Description = "Email already exists"
                }))
            );
        }
        catch
        {
            return IdentityResult.Failed();
        }
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            var repository = unitOfWork.GetRepository<HumanUser, Guid>();
            var dbUser = await repository.TryFindAsync(user.Id, cancellationToken);

            return await dbUser.Match(
                none: () => Task.FromResult(IdentityResult.Failed()),
                some: async u =>
                {
                    repository.Update(user.Id, u with { DisplayName = user.DisplayName });
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return IdentityResult.Success;
                });
        }
        catch
        {
            return IdentityResult.Failed();
        }
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.GetRepository<HumanUser, Guid>().DeleteAsync(user.Id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch
        {
            return IdentityResult.Failed();
        }
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(userId, out Guid id))
            {
                return null;
            }

            return (await unitOfWork.GetRepository<HumanUser, Guid>().TryFindAsync(id, cancellationToken))
                .Match(
                    none: () => null!,
                    some: user => user.ToApplicationUser(id)
                );
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return (await userQueries.GetUserByEmail(normalizedUserName, cancellationToken))
            .Match(
                none: () => null!,
                some: user => user.User.ToApplicationUser(user.Id)
            );
    }

    public async Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        if (email is null)
        {
            return;
        }

        await emailCommands.ChangePrimary(user.Id, email, cancellationToken);
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Emails.FirstOrDefault()?.Value);

    public async Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var emails = await emailQueries.GetEmailsByUserId(user.Id, cancellationToken);

        return emails.FirstOrDefault()?.ConfirmedAt is not null;
    }

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) =>
        emailCommands.SetConfirmed(user.Id, confirmed, cancellationToken);

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var user = await userQueries.GetUserByEmail(normalizedEmail, cancellationToken);
        var repository = unitOfWork.GetRepository<HumanUser, Guid>();

        return await user.Match(
            none: () => Task.FromResult<ApplicationUser?>(null)!,
            some: async u =>
            {
                var password =
                    (await passwordQueries.GetPasswordsFromUserId(u.Id, cancellationToken)).FirstOrDefault(x =>
                        x.IsActive);

                var au = u.User.ToApplicationUser(repository.GetKey((HumanUser)u.User));
                if (password is not null)
                {
                    au.Password = password.Value;
                }

                return au;
            });
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Emails.FirstOrDefault()?.Value.ToUpperInvariant());
    }

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail,
        CancellationToken cancellationToken) => Task.CompletedTask;
}