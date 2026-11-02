using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface INewsCategoryCommands
{
    public sealed record NewsCategoryDraft(string Name, Option<Guid> HeaderImageId);

    Task<NewsCategorySaveResult> Create(NewsCategoryDraft draft, CancellationToken cancellationToken = default);

    Task<NewsCategorySaveResult> Update(
        Guid categoryId,
        NewsCategoryDraft draft,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid categoryId, CancellationToken cancellationToken = default);

    Task<NewsCategorySaveResult> Restore(Guid categoryId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid categoryId, CancellationToken cancellationToken = default);
}
