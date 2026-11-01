using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IRoleCommands
{
    Task CreateWithId(Guid id, Role role, CancellationToken cancellationToken = default);

    Task<Option<Guid>> Create(string name, CancellationToken cancellationToken = default);

    Task Delete(Guid roleId, CancellationToken cancellationToken = default);

    Task<bool> Restore(Guid roleId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid roleId, CancellationToken cancellationToken = default);

    Task AssignBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default);

    Task RevokeBuiltIn(Guid userId, BuiltInRole role, CancellationToken cancellationToken = default);
}
