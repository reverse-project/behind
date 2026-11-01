using Behind.Core.Domain;

namespace Behind.Core.Database.Queries;

public sealed class AccessQueries(AbstractCoreContext context) : IAccessQueries
{
    public Task<UserAccess> GetUserAccess(Guid userId, CancellationToken cancellationToken = default) =>
        context.GetUserAccessAsync(userId, cancellationToken);
}
