using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IWallOfShameCommands
{
    Task Ban(Guid userId, Guid givenBy, Reason reason, CancellationToken cancellationToken = default);

    Task<bool> Unban(Guid userId, CancellationToken cancellationToken = default);
}
