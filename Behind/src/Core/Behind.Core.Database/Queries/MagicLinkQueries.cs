using Behind.Common;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class MagicLinkQueries(AbstractCoreContext context) : IMagicLinkQueries
{
    public Task<bool> IsUsableForFunction(
        TokenNonce nonce,
        Guid userId,
        MagicLinkFunction function,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        context.MagicLinks.AnyAsync(
            link => link.Nonce == nonce
                    && EF.Property<Guid>(link, "UserId") == userId
                    && link.Function == function
                    && link.IssuedAt <= now
                    && link.ExpiresAt > now
                    && EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty) == null,
            cancellationToken);

    public Task<bool> HasIssuedSince(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        context.MagicLinks.AnyAsync(
            link => EF.Property<Guid>(link, "UserId") == userId
                    && (link.Function == MagicLinkFunction.SignIn || link.Function == MagicLinkFunction.LostPassword)
                    && link.IssuedAt > since,
            cancellationToken);

    public async Task<Option<Guid>> FindPasswordResetUser(
        TokenNonce nonce,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var userIds = await context.MagicLinks
            .Where(link => link.Nonce == nonce
                           && link.Function == MagicLinkFunction.PasswordReset
                           && EF.Property<DateTimeOffset?>(link, MagicLinkConfiguration.ConsumedAtProperty) == null
                           && link.ExpiresAt > now)
            .Select(link => EF.Property<Guid>(link, "UserId"))
            .Take(1)
            .ToListAsync(cancellationToken);

        return userIds is [var userId] ? Some(userId) : None;
    }
}
