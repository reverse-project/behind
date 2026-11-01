using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IRoleQueries
{
    public sealed record RoleResult(Guid Id, Role Role);
    Task<Option<RoleResult>> GetRoleByName(string name, CancellationToken cancellationToken = default);

    Task<Option<RoleResult>> GetRoleById(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleResult>> ListRoles(CancellationToken cancellationToken = default);

    Task<PagedResult<RoleResult>> PageRoles(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetRolesByUserId(Guid userId, CancellationToken cancellationToken = default);
}
