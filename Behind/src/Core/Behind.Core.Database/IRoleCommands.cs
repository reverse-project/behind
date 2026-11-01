using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IRoleCommands
{
    Task CreateWithId(Guid id, Role role, CancellationToken cancellationToken = default);

    Task AssignBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default);

    Task RevokeBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default);
}
