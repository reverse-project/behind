using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISiteImageQueries
{
    Task<Option<SiteImage>> Find(Guid id, CancellationToken cancellationToken = default);
}
