using Behind.Core.Domain;

namespace Behind.Core.Database.Commands;

public sealed class AccessCommands(AbstractCoreContext context) : IAccessCommands
{
    public Task SetRoleAccess(Guid roleId, AccessSelection selection, CancellationToken cancellationToken = default) =>
        context.ReplaceRoleAccessAsync(roleId, selection, cancellationToken);

    public Task SetUserAccess(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        AccessSelection selection,
        CancellationToken cancellationToken = default) =>
        context.ReplaceUserAccessAsync(userId, roleIds, selection, cancellationToken);
}
