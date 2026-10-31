using Behind.Common;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class MagicLinkCommands(AbstractCoreContext context) : IMagicLinkCommands
{
    public async Task Issue(Guid userId, MagicLink magicLink, CancellationToken cancellationToken = default)
    {
        var entry = await context.MagicLinks.AddAsync(magicLink, cancellationToken);
        entry.Property<Guid>("UserId").CurrentValue = userId;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Option<Guid>> Consume(
        TokenNonce nonce,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        var consumed = await context.MagicLinks
            .Where(link => link.Nonce == nonce
                           && EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty) == null
                           && link.ExpiresAt > consumedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    link => EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty),
                    consumedAt),
                cancellationToken);

        if (consumed == 0)
        {
            return None;
        }

        return await context.MagicLinks
            .Where(link => link.Nonce == nonce)
            .Select(link => EF.Property<Guid>(link, "UserId"))
            .FirstAsync(cancellationToken);
    }

    public Task<int> DeleteExpired(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.MagicLinks
            .Where(link => link.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
}
