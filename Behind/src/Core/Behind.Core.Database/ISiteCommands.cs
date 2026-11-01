using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISiteCommands
{
    Task<SiteSaveResult> Create(
        string title,
        string slug,
        string content,
        bool publish,
        CancellationToken cancellationToken = default);

    Task<SiteSaveResult> SaveVersion(
        Guid siteId,
        string title,
        string slug,
        string content,
        bool publish,
        CancellationToken cancellationToken = default);

    Task<SiteSaveResult> Activate(VersionedKey<Guid> id, CancellationToken cancellationToken = default);
}
