using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IMenuCommands
{
    public sealed record MenuDraft(string Name, string Title);

    Task<MenuSaveResult> Create(MenuDraft draft, CancellationToken cancellationToken = default);

    Task<MenuSaveResult> Update(Guid menuId, MenuDraft draft, CancellationToken cancellationToken = default);

    Task<MenuRemovalResult> Delete(Guid menuId, CancellationToken cancellationToken = default);

    Task<MenuSaveResult> Restore(Guid menuId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid menuId, CancellationToken cancellationToken = default);

    Task<bool> Ensure(Guid menuId, MenuDraft draft, CancellationToken cancellationToken = default);
}
