using Behind.Core.Domain;

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
}