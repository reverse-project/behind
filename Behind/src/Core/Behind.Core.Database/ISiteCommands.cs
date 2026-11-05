using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISiteCommands
{
    public sealed record SiteSectionDraft(
        SiteSectionTheme Theme,
        string Title,
        string Content,
        Option<Guid> ImageId,
        string ImageAlt,
        SiteSectionImagePosition ImagePosition);

    Task<SiteSaveResult> Create(
        string title,
        string slug,
        string content,
        bool publish,
        bool hideTitle = false,
        IReadOnlyList<SiteSectionDraft>? sections = null,
        CancellationToken cancellationToken = default);

    Task<SiteSaveResult> SaveVersion(
        Guid siteId,
        string title,
        string slug,
        string content,
        bool publish,
        bool hideTitle = false,
        IReadOnlyList<SiteSectionDraft>? sections = null,
        CancellationToken cancellationToken = default);

    Task<SiteSaveResult> Activate(VersionedKey<Guid> id, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid siteId, CancellationToken cancellationToken = default);

    Task<SiteSaveResult> Restore(Guid siteId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid siteId, CancellationToken cancellationToken = default);
}
