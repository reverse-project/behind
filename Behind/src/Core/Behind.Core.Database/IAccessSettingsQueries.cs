using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IAccessSettingsQueries
{
    Task<AccessSettings> Current(CancellationToken cancellationToken = default);

    Task<Option<AccessSettings>> FindDeleted(CancellationToken cancellationToken = default);
}
