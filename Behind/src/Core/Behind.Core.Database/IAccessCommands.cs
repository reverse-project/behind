using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IAccessCommands
{
    Task SetRoleAccess(Guid roleId, AccessSelection selection, CancellationToken cancellationToken = default);

    Task SetUserAccess(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        AccessSelection selection,
        CancellationToken cancellationToken = default);
}
