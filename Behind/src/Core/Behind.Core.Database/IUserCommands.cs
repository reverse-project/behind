using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IUserCommands
{
    Task CreateWithId(Guid id, HumanUser user, CancellationToken cancellationToken = default);

    Task<bool> ChangeDisplayName(Guid userId, string displayName, CancellationToken cancellationToken = default);
}
