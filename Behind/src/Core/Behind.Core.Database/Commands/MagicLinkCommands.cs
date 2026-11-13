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

    public async Task<bool> TryIssue(Guid userId, MagicLink magicLink, CancellationToken cancellationToken = default)
    {
        var entry = await context.MagicLinks.AddAsync(magicLink, cancellationToken);
        entry.Property<Guid>("UserId").CurrentValue = userId;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            entry.State = EntityState.Detached;

            if (await context.MagicLinks.AnyAsync(link => link.Nonce == magicLink.Nonce, cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    public async Task<bool> LockForFunction(
        TokenNonce nonce,
        Guid userId,
        MagicLinkFunction function,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        await context.MagicLinks
            .Where(link => link.Nonce == nonce
                           && EF.Property<Guid>(link, "UserId") == userId
                           && link.Function == function
                           && link.IssuedAt <= now
                           && EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty) == null
                           && link.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(link => link.IssuedAt, link => link.IssuedAt),
                cancellationToken) == 1;

    public async Task<bool> ConsumeForFunction(
        TokenNonce nonce,
        Guid userId,
        MagicLinkFunction function,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default) =>
        await context.MagicLinks
            .Where(link => link.Nonce == nonce
                           && EF.Property<Guid>(link, "UserId") == userId
                           && link.Function == function
                           && link.IssuedAt <= consumedAt
                           && EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty) == null
                           && link.ExpiresAt > consumedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    link => EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty),
                    consumedAt),
                cancellationToken) == 1;

    public async Task<Option<MagicLinkRedemption>> Consume(
        TokenNonce nonce,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        var consumed = await context.MagicLinks
            .Where(link => link.Nonce == nonce
                           && (link.Function == MagicLinkFunction.SignIn || link.Function == MagicLinkFunction.LostPassword)
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
            .Select(link => new MagicLinkRedemption(EF.Property<Guid>(link, "UserId"), link.Function))
            .FirstAsync(cancellationToken);
    }

    public async Task<Option<Guid>> ConsumePasswordReset(
        TokenNonce nonce,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        var consumed = await context.MagicLinks
            .Where(link => link.Nonce == nonce
                           && link.Function == MagicLinkFunction.PasswordReset
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

    public Task<int> RevokeForUser(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default) =>
        context.MagicLinks
            .Where(link => EF.Property<Guid>(link, "UserId") == userId
                           && EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty) == null
                           && link.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    link => EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty),
                    revokedAt),
                cancellationToken);

    public Task<int> DeleteExpired(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.MagicLinks
            .Where(link => link.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
}
