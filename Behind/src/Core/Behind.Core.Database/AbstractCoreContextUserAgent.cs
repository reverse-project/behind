using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<UserAgent> UserAgents => Set<UserAgent>();

    private async Task<Guid> GetOrAddUserAgentKeyAsync(UserAgent userAgent, CancellationToken cancellationToken) =>
        FindTrackedUserAgentKey(userAgent)
        ?? await FindUserAgentKeyQuery(userAgent).FirstOrDefaultAsync(cancellationToken)
        ?? AddUserAgent(userAgent);

    private Guid GetOrAddUserAgentKey(UserAgent userAgent) =>
        FindTrackedUserAgentKey(userAgent)
        ?? FindUserAgentKeyQuery(userAgent).FirstOrDefault()
        ?? AddUserAgent(userAgent);

    private Guid? FindTrackedUserAgentKey(UserAgent userAgent) =>
        UserAgents.Local
            .Where(tracked => tracked.Value == userAgent.Value)
            .Select(tracked => (Guid?)Entry(tracked).Property<Guid>("Id").CurrentValue)
            .FirstOrDefault();

    private IQueryable<Guid?> FindUserAgentKeyQuery(UserAgent userAgent)
    {
        var value = userAgent.Value;
        return UserAgents
            .Where(stored => stored.Value == value)
            .Select(stored => (Guid?)EF.Property<Guid>(stored, "Id"));
    }

    private Guid AddUserAgent(UserAgent userAgent) =>
        UserAgents.Add(userAgent).Property<Guid>("Id").CurrentValue;
}
