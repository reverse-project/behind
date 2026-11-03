using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IMenuQueries
{
    public sealed record MenuListing(VersionedKey<Guid> Id, string Name, string Title, long ItemCount);

    public sealed record MenuItemView(
        Guid Id,
        string Title,
        string Url,
        MenuTarget Target,
        MenuItemKind Kind,
        IReadOnlyList<MenuItemView> Children);

    public sealed record MenuView(Guid Id, string Name, string Title, IReadOnlyList<MenuItemView> Items);

    Task<PagedResult<MenuListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<MenuListing>> Find(
        Guid menuId,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuView>> Resolve(
        IReadOnlyCollection<string> names,
        Option<Guid> viewerId,
        CancellationToken cancellationToken = default);
}
