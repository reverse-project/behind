using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IMenuItemCommands
{
    public sealed record MenuItemDraft(
        string Title,
        string Url,
        MenuTarget Target,
        MenuItemKind Kind,
        int Position,
        Option<Guid> ParentId,
        IReadOnlyList<Guid> RoleIds);

    Task<MenuItemSaveResult> Create(
        Guid menuId,
        MenuItemDraft draft,
        CancellationToken cancellationToken = default);

    Task<MenuItemSaveResult> Update(
        Guid itemId,
        MenuItemDraft draft,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid itemId, CancellationToken cancellationToken = default);

    Task<bool> Restore(Guid itemId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid itemId, CancellationToken cancellationToken = default);

    Task<bool> Ensure(
        Guid itemId,
        Guid menuId,
        MenuItemDraft draft,
        CancellationToken cancellationToken = default);
}
