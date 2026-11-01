using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class UserCommands(AbstractCoreContext context) : IUserCommands
{
    public async Task CreateWithId(Guid id, HumanUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var repository = context.GetRepository<HumanUser, Guid>();
        await repository.AddAsync(user, cancellationToken);
        context.Entry(user).Property<Guid>("Id").CurrentValue = id;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ChangeDisplayName(
        Guid userId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var repository = context.GetRepository<HumanUser, Guid>();
        var found = await repository.TryFindAsync(userId, cancellationToken);

        return await found.Match(
            () => Task.FromResult(false),
            async user =>
            {
                repository.Update(userId, user with { DisplayName = displayName.ToDisplayName() });
                await context.SaveChangesAsync(cancellationToken);
                return true;
            });
    }

    public async Task Delete(Guid userId, CancellationToken cancellationToken = default)
    {
        await context.GetRepository<HumanUser, Guid>().DeleteAsync(userId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Restore(Guid userId, CancellationToken cancellationToken = default)
    {
        await context.GetRestoreRepository<HumanUser, Guid>().RestoreAsync(userId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HardDelete(Guid userId, CancellationToken cancellationToken = default)
    {
        await context.GetHardDeleteRepository<HumanUser, Guid>().HardDeleteAsync(userId, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
