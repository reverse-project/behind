using static Behind.Common.FunctionalExtensions;
using Behind.Common.Logging;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

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
                    await roleCommands.AssignBuiltIn(user.Id, BuiltInAccess.Users, cancellationToken);

                    if (user.PendingApproval)
                    {
                        await approvalCommands.RequireApproval(user.Id, cancellationToken);
                    }

                    return IdentityResult.Success;
                },
                some: _ =>
                {
                    logger.LogWarning("User {UserId} was not created: the email already exists", () => [user.Id]);

                    return Task.FromResult(IdentityResult.Failed(new IdentityError
                    {
                        Code = "EmailAlreadyExists",
                        Description = "Email already exists"
                    }));
                }
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to create user {UserId}", () => [user.Id]);

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
                none: () =>
                {
                    logger.LogWarning("User {UserId} was not updated: it does not exist", () => [user.Id]);

                    return Task.FromResult(IdentityResult.Failed());
                },
                some: async u =>
                {
                    repository.Update(user.Id, u with { DisplayName = user.DisplayName });
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return IdentityResult.Success;
                });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to update user {UserId}", () => [user.Id]);

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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to delete user {UserId}", () => [user.Id]);

            return IdentityResult.Failed();
        }
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(userId, out Guid id))
            {
                logger.LogDebug("User lookup ignored: {UserId} is not a valid identifier", () => [userId]);

                return null;
            }

            return await (await unitOfWork.GetRepository<HumanUser, Guid>().TryFindAsync(id, cancellationToken))
                .Match(
                    none: () => Task.FromResult<ApplicationUser?>(null),
                    some: async user => await user.ToApplicationUser(id).WithApproval(approvalQueries, cancellationToken)
                );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to find user {UserId}", () => [userId]);

            return null;
        }
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return await (await userQueries.GetUserByEmail(normalizedUserName, cancellationToken))
            .Match(
                none: () => Task.FromResult<ApplicationUser?>(null),
                some: async user => await user.User.ToApplicationUser(user.Id)
                    .WithApproval(approvalQueries, cancellationToken)
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

        return await user.Match(
            none: () => Task.FromResult<ApplicationUser?>(null)!,
            some: async u =>
            {
                var password =
                    (await passwordQueries.GetPasswordsFromUserId(u.Id, cancellationToken)).FirstOrDefault(x =>
                        x.IsActive);

                var au = await u.User.ToApplicationUser(u.Id).WithApproval(approvalQueries, cancellationToken);
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