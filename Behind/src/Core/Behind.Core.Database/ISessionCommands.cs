using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISessionCommands
{
    Task<Guid> CreateForUserId(Guid userId, ValidSession session, CancellationToken cancellationToken = default);
}
