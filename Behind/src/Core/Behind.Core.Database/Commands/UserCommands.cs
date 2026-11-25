using Behind.Common.Database;
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
                var tracked = await context.Users.SingleAsync(value => EF.Property<Guid>(value, "Id") == userId, cancellationToken);
                context.Entry(tracked).Property(value => value.DisplayName).CurrentValue = displayName.ToDisplayName();
                await context.SaveChangesAsync(cancellationToken);
                return true;
            });
    }

    public async Task Delete(Guid userId, CancellationToken cancellationToken = default)
    {
        await context.GetRepository<HumanUser, Guid>().DeleteAsync(userId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnsetSwitchPin(Guid userId, CancellationToken cancellationToken = default)
    {
        var pin = await context.UserSwitchPins
            .SingleOrDefaultAsync(record => EF.Property<Guid>(record, "UserId") == userId, cancellationToken);
        if (pin is null)
        {
            return;
        }

        context.UserSwitchPins.Remove(pin);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Restore(Guid userId, CancellationToken cancellationToken = default)
    {
        await context.GetRestoreRepository<HumanUser, Guid>().RestoreAsync(userId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IUserCommands.HardDeleteOutcome> HardDelete(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await context.Subaccounts.AnyAsync(link => EF.Property<Guid>(link, "PrimaryUserId") == userId, cancellationToken))
        {
            return IUserCommands.HardDeleteOutcome.LinkedSubaccounts;
        }

        using (context.SoftDelete.IncludeDeleted())
        {
            var releasedLinks = await context.Subaccounts
                .Where(link => EF.Property<Guid>(link, "PrimaryUserId") == userId)
                .ToListAsync(cancellationToken);
            foreach (var link in releasedLinks)
            {
                context.HardDelete(link);
            }
        }

        await context.GetHardDeleteRepository<HumanUser, Guid>().HardDeleteAsync(userId, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return IUserCommands.HardDeleteOutcome.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return IUserCommands.HardDeleteOutcome.Rejected;
        }
    }
}
