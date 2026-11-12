using Behind.Common;

namespace Behind.Core.Database;

public interface IWallOfShameQueries
{
    public sealed record ActiveBan(string Reason, Option<DateTimeOffset> Expires);

    public sealed record ShamedUser(string DisplayName, string Reason, Option<DateTimeOffset> Expires)
    {
        public Guid Id { get; init; }
    }

    Task<Option<ActiveBan>> FindActiveBan(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<PagedResult<ShamedUser>> GetActive(Flipper flipper, DateTimeOffset now, CancellationToken cancellationToken = default);
}
