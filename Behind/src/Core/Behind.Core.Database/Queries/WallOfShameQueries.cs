using Behind.Common;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class WallOfShameQueries(AbstractCoreContext context) : IWallOfShameQueries
{
    public async Task<Option<IWallOfShameQueries.ActiveBan>> FindActiveBan(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.NewestBans(context.WallOfShameEntries
            .ActiveAt(now)
            .ForUser(userId))
            .Select(entry => new BanRow(entry.Reason, EF.Property<DateTimeOffset?>(entry, "ExpiresAt")))
            .Take(1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Head().Map(row => row.ToActiveBan());
    }

    public async Task<PagedResult<IWallOfShameQueries.ShamedUser>> GetActive(
        Flipper flipper,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var active = context.WallOfShameEntries.ActiveAt(now);
        var total = await active.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(
                from entry in context.NewestBans(active)
                join user in context.Users
                    on EF.Property<Guid>(entry, "UserId") equals EF.Property<Guid>(user, "Id")
                select new ShamedRow(EF.Property<Guid>(user, "Id"), user.DisplayName, entry.Reason, EF.Property<DateTimeOffset?>(entry, "ExpiresAt")))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<IWallOfShameQueries.ShamedUser>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IWallOfShameQueries.ShamedUser>>(rows.Select(row => row.ToShamedUser()).ToList())
                : None);
    }

    private sealed record BanRow(Reason Reason, DateTimeOffset? Expires)
    {
        public IWallOfShameQueries.ActiveBan ToActiveBan() => new(Reason.Value, Expires.ToOption());
    }

    private sealed record ShamedRow(Guid Id, string DisplayName, Reason Reason, DateTimeOffset? Expires)
    {
        public IWallOfShameQueries.ShamedUser ToShamedUser() =>
            new(Id.ToUserLabel(DisplayName), Reason.Value, Expires.ToOption()) { Id = Id };
    }
}
