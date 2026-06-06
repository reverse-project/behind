using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IPasswordCommands
{
    Task InactivateForUser(User user, CancellationToken cancellationToken = default);
    
    Task InactivateFromUserId(Guid id, CancellationToken cancellationToken = default);
    
    Task CreateForUser(User user, Password password, CancellationToken cancellationToken = default);
    
    Task CreateFromUserId(Guid id, Password password, CancellationToken cancellationToken = default);
}