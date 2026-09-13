using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IRoleQueries
{
    public sealed record RoleResult(Guid Id, Role Role);
    Task<Option<RoleResult>> GetRoleByName(string name, CancellationToken cancellationToken = default);
}