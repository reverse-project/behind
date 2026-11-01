using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public partial class AbstractCoreContext : IRestoreRepository<HumanUser, Guid>
{
    Task IRestoreRepository<HumanUser, Guid>.RestoreAsync(Guid key, CancellationToken cancellationToken) =>
        this.RestoreByKeyAsync<HumanUser>([key], cancellationToken);
}
