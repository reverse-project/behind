using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class UserApprovalCommands(AbstractCoreContext context) : IUserApprovalCommands
{
    public async Task RequireApproval(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await FindPending(userId, cancellationToken) is not null)
        {
            return;
        }

        await Pending.AddAsync(new Dictionary<string, object> { [UserApprovalStore.UserId] = userId }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Approve(Guid userId, CancellationToken cancellationToken = default)
    {
        var pending = await FindPending(userId, cancellationToken);

        if (pending is null)
        {
            return false;
        }

        Pending.Remove(pending);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private DbSet<Dictionary<string, object>> Pending =>
        context.Set<Dictionary<string, object>>(UserApprovalStore.Table);

    private Task<Dictionary<string, object>?> FindPending(Guid userId, CancellationToken cancellationToken) =>
        Pending
            .Where(row => EF.Property<Guid>(row, UserApprovalStore.UserId) == userId)
            .FirstOrDefaultAsync(cancellationToken);
}
