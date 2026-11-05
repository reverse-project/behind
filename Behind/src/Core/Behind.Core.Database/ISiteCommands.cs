using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISiteCommands
{
    Task<SiteSaveResult> Create(
        string title,
        string slug,
        string content,
        bool publish,
        bool hideTitle = false,
        CancellationToken cancellationToken = default);

    Task<SiteSaveResult> SaveVersion(
        Guid siteId,
        string title,
        string slug,
        string content,
        bool publish,
        bool hideTitle = false,
        CancellationToken cancellationToken = default);

    Task<SiteSaveResult> Activate(VersionedKey<Guid> id, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid siteId, CancellationToken cancellationToken = default);

    Task<SiteSaveResult> Restore(Guid siteId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid siteId, CancellationToken cancellationToken = default);
}
