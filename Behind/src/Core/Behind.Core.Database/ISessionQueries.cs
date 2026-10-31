using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISessionQueries
{
    Task<Option<Session>> TryFindById(Guid id, CancellationToken cancellationToken = default);
    Task<Option<(Session Session, Guid UserId)>> TryFindByIdWithUserId(Guid id, CancellationToken cancellationToken = default);
}
