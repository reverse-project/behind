using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IPasswordQueries
{
    Task<IEnumerable<Password>> GetPasswordsForUser(User user, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Password>> GetPasswordsFromUserId(Guid id, CancellationToken cancellationToken = default);
}