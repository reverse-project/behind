using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class MagicLinkQueries(AbstractCoreContext context) : IMagicLinkQueries
{
    public Task<bool> HasIssuedSince(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        context.MagicLinks.AnyAsync(
            link => EF.Property<Guid>(link, "UserId") == userId && link.IssuedAt > since,
            cancellationToken);
}
