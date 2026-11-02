using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IArticleCommands
{
    public sealed record ArticleDraft(string Title, string Summary, string Content, Option<Guid> CategoryId);

    Task<ArticleSaveResult> Create(
        Guid authorId,
        ArticleDraft draft,
        bool publish,
        CancellationToken cancellationToken = default);

    Task<ArticleSaveResult> SaveVersion(
        Guid articleId,
        ArticleDraft draft,
        bool publish,
        CancellationToken cancellationToken = default);

    Task<ArticleSaveResult> Activate(VersionedKey<Guid> id, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid articleId, CancellationToken cancellationToken = default);

    Task<bool> Restore(Guid articleId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid articleId, CancellationToken cancellationToken = default);
}
