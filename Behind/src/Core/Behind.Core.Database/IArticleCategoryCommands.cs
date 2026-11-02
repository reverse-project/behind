using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IArticleCategoryCommands
{
    public sealed record ArticleCategoryDraft(string Name, Option<Guid> HeaderImageId);

    Task<ArticleCategorySaveResult> Create(
        ArticleCategoryDraft draft,
        CancellationToken cancellationToken = default);

    Task<ArticleCategorySaveResult> Update(
        Guid categoryId,
        ArticleCategoryDraft draft,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid categoryId, CancellationToken cancellationToken = default);

    Task<ArticleCategorySaveResult> Restore(Guid categoryId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid categoryId, CancellationToken cancellationToken = default);
}
