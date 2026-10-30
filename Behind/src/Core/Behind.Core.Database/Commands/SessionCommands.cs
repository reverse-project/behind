using Behind.Core.Domain;

namespace Behind.Core.Database.Commands;

public sealed class SessionCommands(AbstractCoreContext context) : ISessionCommands
{
    public async Task<Guid> CreateForUserId(
        Guid userId,
        ValidSession session,
        CancellationToken cancellationToken = default)
    {
        await context.GetRepository<Session, Guid>().AddAsync(session, cancellationToken);

        var entry = context.Entry(session);
        entry.Property<Guid>("UserId").CurrentValue = userId;
        await context.SaveChangesAsync(cancellationToken);

        return entry.Property<Guid>("Id").CurrentValue;
    }
}
