using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public sealed record ServiceUserEntry(Guid Id, long Version, ServiceUser User);

public sealed record ServiceUserCredential(ServiceUserEntry User, string ApiKey);

public interface IServiceUserStore
{
    Task<IReadOnlyList<ServiceUserEntry>> List(bool deleted, CancellationToken cancellationToken);
    Task<Option<ServiceUserEntry>> Find(Guid id, CancellationToken cancellationToken);
    Task<Option<ServiceUserEntry>> Authenticate(string apiKey, CancellationToken cancellationToken);
    Task<Option<ServiceUserCredential>> Create(string name, CancellationToken cancellationToken);
    Task<Option<ServiceUserCredential>> Rotate(Guid id, long version, CancellationToken cancellationToken);
    Task<bool> SetEnabled(Guid id, long version, bool enabled, CancellationToken cancellationToken);
    Task<bool> Delete(Guid id, long version, CancellationToken cancellationToken);
    Task<bool> Restore(Guid id, CancellationToken cancellationToken);
    Task<bool> HardDelete(Guid id, CancellationToken cancellationToken);
}
