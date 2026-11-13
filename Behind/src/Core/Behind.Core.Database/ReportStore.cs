using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed class ReportStore(AbstractCoreContext context)
{
    public sealed record Page(long Total, int PageNumber, int PageSize, long PageCount, IReadOnlyList<UserReport> Items);

    public async Task<Page> List(
        string? status, int page, int pageSize, bool deleted, CancellationToken cancellationToken)
    {
        using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
        var state = deleted ? DeletionState.Deleted : DeletionState.Active;
        var rows = context.UserReports.InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.UserReports);
        if (status is not null)
        {
            rows = rows.Where(report => report.Status == status);
        }

        var total = await rows.LongCountAsync(cancellationToken);
        var items = await context.OrderByCreation(rows, true).Skip((page - 1) * pageSize).Take(pageSize)
            .ToKeyedAsync(cancellationToken);
        return new Page(total, page, pageSize, (total + pageSize - 1) / pageSize,
            items.Select(item => item.Entity with { Id = new(item.Key, item.Version) }).ToArray());
    }

    public async Task<UserReport?> Find(Guid id, bool deleted, CancellationToken cancellationToken)
    {
        using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
        var state = deleted ? DeletionState.Deleted : DeletionState.Active;
        var item = (await context.UserReports.InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.UserReports).OfKey(id).ToKeyedAsync(cancellationToken)).SingleOrDefault();
        return item is null ? null : item.Entity with { Id = new(item.Key, item.Version) };
    }

    public async Task Save(UserReport report, CancellationToken cancellationToken)
    {
        var tracked = context.Add(report);
        tracked.StampVersionedKey(report.Id.Key, report.Id.Version);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReportEmailDelivery>> Deliveries(Guid reportId, CancellationToken cancellationToken)
    {
        var items = await context.ReportEmailDeliveries.LatestOf(context.ReportEmailDeliveries)
            .Where(delivery => delivery.ReportId == reportId).ToKeyedAsync(cancellationToken);
        return items.Select(item => item.Entity with { Id = new(item.Key, item.Version) }).ToArray();
    }

    public void StageDelivery(ReportEmailDelivery delivery)
    {
        var tracked = context.Add(delivery);
        tracked.StampVersionedKey(delivery.Id.Key, delivery.Id.Version);
    }

    public async Task<bool> Delete(Guid id, string action, CancellationToken cancellationToken)
    {
        var rows = context.UserReports.OfKey(id);
        var changed = action switch
        {
            "delete" => await context.MarkSoftDeletedAsync(rows, cancellationToken),
            "restore" => await context.MarkRestoredAsync(rows, cancellationToken),
            "hard/delete" => await context.MarkHardDeletedAsync(rows, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        if (changed == 0)
        {
            return false;
        }

        if (action == "hard/delete")
        {
            using var scope = context.SoftDelete.IncludeDeleted();
            foreach (var delivery in await context.ReportEmailDeliveries
                         .Where(delivery => delivery.ReportId == id).ToListAsync(cancellationToken))
            {
                context.HardDelete(delivery);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
