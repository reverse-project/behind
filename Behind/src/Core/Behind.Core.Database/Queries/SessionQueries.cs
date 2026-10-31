using Behind.Common;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class SessionQueries(AbstractCoreContext context) : ISessionQueries
{
    public Task<Option<Session>> TryFindById(Guid id, CancellationToken cancellationToken = default) =>
        context.GetRepository<Session, Guid>().TryFindAsync(id, cancellationToken);

    public async Task<Option<(Session Session, Guid UserId)>> TryFindByIdWithUserId(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        var userId = await context.Sessions
            .Where(s => EF.Property<Guid>(s, "Id") == id)
            .Select(s => (Guid?)EF.Property<Guid>(s, "UserId"))
            .FirstOrDefaultAsync(cancellationToken);

        if (userId is null)
        {
            return None;
        }

        var session = await TryFindById(id, cancellationToken);
        return session.Map(s => (s, userId.Value));
    }
}
