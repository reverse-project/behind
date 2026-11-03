using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IMenuItemQueries
{
    public sealed record MenuItemListing(
        VersionedKey<Guid> Id,
        Guid MenuId,
        Option<Guid> ParentId,
        string Title,
        string Url,
        MenuTarget Target,
        MenuItemKind Kind,
        int Position,
        IReadOnlyList<Guid> RoleIds);

    Task<PagedResult<MenuItemListing>> ListOfMenu(
        Guid menuId,
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Guid menuId,
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuItemListing>> ListParents(Guid menuId, CancellationToken cancellationToken = default);
}
