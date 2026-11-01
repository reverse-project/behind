using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRestoreRepository<HumanUser, Guid>, IRestoreRepository<UserRole, Guid>
{
    Task IRestoreRepository<HumanUser, Guid>.RestoreAsync(Guid key, CancellationToken cancellationToken) =>
        this.RestoreByKeyAsync<HumanUser>([key], cancellationToken);

    Task IRestoreRepository<UserRole, Guid>.RestoreAsync(Guid key, CancellationToken cancellationToken) =>
        this.RestoreByKeyAsync<UserRole>([key], cancellationToken);
}
