using Behind.Core.Domain;

namespace Behind.Core.Database.Commands;

public sealed class UserCommands(AbstractCoreContext context) : IUserCommands
{
    public Task CreateWithId(Guid id, HumanUser user, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            throw new ArgumentNullException(nameof(user));
        }
        
        var repository = context.GetRepository<HumanUser, Guid>();
        repository.AddAsync(user, cancellationToken);
        context.Entry(user).Property<Guid>("Id").CurrentValue = id;

        return Task.CompletedTask;
    }
}