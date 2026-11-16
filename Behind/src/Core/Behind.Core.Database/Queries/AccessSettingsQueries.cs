using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class AccessSettingsQueries(AbstractCoreContext context) : IAccessSettingsQueries
{
    public async Task<AccessSettings> Current(CancellationToken cancellationToken = default) =>
        (await Find(DeletionState.Active, cancellationToken)).GetOrElse(AccessSettings.Open);

    public async Task<Option<AccessSettings>> FindDeleted(CancellationToken cancellationToken = default)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await Find(DeletionState.Deleted, cancellationToken);
    }

    private async Task<Option<AccessSettings>> Find(DeletionState state, CancellationToken cancellationToken) =>
        (await context.LatestAccessSettingsIn(state)
            .ToRows()
            .Take(1)
            .AsNoTracking()
            .ToListAsync(cancellationToken))
        .Head()
        .Map(row => row.ToSettings());
}
