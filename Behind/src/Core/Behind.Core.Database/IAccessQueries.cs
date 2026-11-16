using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IAccessQueries
{
    Task<UserAccess> GetUserAccess(Guid userId, CancellationToken cancellationToken = default);
}
