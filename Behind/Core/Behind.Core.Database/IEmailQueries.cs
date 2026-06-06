using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IEmailQueries
{
    Task<Option<Email>> GetEmailByValue(string value, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Email>> GetEmailsByUser(User user, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Email>> GetEmailsByUserId(Guid id, CancellationToken cancellationToken = default);
}