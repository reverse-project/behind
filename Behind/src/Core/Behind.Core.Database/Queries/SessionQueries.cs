using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database.Queries;

public sealed class SessionQueries(AbstractCoreContext context) : ISessionQueries
{
    public Task<Option<Session>> TryFindById(Guid id, CancellationToken cancellationToken = default) =>
        context.GetRepository<Session, Guid>().TryFindAsync(id, cancellationToken);
}
