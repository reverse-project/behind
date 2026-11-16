using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class WallOfShameCommands(AbstractCoreContext context) : IWallOfShameCommands
{
    public Task Ban(Guid userId, Guid givenBy, Reason reason, CancellationToken cancellationToken = default) =>
        BanUntil(userId, givenBy, reason, null, cancellationToken);

    public async Task BanUntil(
        Guid userId, Guid givenBy, Reason reason, DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        context.WallOfShameEntries.RemoveRange(await EntriesOf(userId, cancellationToken));

        var entry = new WallOfShameEntry(reason);
        await context.WallOfShameEntries.AddAsync(entry, cancellationToken);

        var tracked = context.Entry(entry);
        tracked.Property<Guid>("UserId").CurrentValue = userId;
        tracked.Property<Guid>("GivenById").CurrentValue = givenBy;
        tracked.Property<DateTimeOffset?>("ExpiresAt").CurrentValue = expiresAt;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Unban(Guid userId, CancellationToken cancellationToken = default)
    {
        var entries = await EntriesOf(userId, cancellationToken);

        if (entries.Count == 0)
        {
            return false;
        }

        context.WallOfShameEntries.RemoveRange(entries);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task<List<WallOfShameEntry>> EntriesOf(Guid userId, CancellationToken cancellationToken) =>
        context.WallOfShameEntries.ForUser(userId).ToListAsync(cancellationToken);
}
