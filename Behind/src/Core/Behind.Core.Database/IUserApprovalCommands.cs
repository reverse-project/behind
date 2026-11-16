namespace Behind.Core.Database;

public interface IUserApprovalCommands
{
    Task RequireApproval(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> Approve(Guid userId, CancellationToken cancellationToken = default);
}
