using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IUserQueries
{
    public sealed record UserResult(Guid Id, User User);
    
    Task<Option<UserResult>> GetUserByEmail(string email, CancellationToken cancellationToken = default);
}