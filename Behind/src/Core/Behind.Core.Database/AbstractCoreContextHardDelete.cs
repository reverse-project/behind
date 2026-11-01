using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public partial class AbstractCoreContext :
    IHardDeleteRepository<HumanUser, Guid>,
    IHardDeleteRepository<Email, Guid>,
    IHardDeleteRepository<UserRole, Guid>,
    IHardDeleteRepository<Grant, Guid>,
    IHardDeleteRepository<Restriction, Guid>,
    IHardDeleteRepository<Password, Guid>,
    IHardDeleteRepository<WallOfShameEntry, Guid>
{
    void IHardDeleteRepository<HumanUser, Guid>.HardDelete(HumanUser aggregate) =>
        this.HardDelete(aggregate);

    Task IHardDeleteRepository<HumanUser, Guid>.HardDeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.HardDeleteByKeyAsync<HumanUser>([key], cancellationToken);

    void IHardDeleteRepository<Email, Guid>.HardDelete(Email aggregate) =>
        this.HardDelete(aggregate);

    Task IHardDeleteRepository<Email, Guid>.HardDeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.HardDeleteByKeyAsync<Email>([key], cancellationToken);

    void IHardDeleteRepository<UserRole, Guid>.HardDelete(UserRole aggregate) =>
        this.HardDelete(aggregate);

    Task IHardDeleteRepository<UserRole, Guid>.HardDeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.HardDeleteByKeyAsync<UserRole>([key], cancellationToken);

    void IHardDeleteRepository<Grant, Guid>.HardDelete(Grant aggregate) =>
        this.HardDelete(aggregate);

    Task IHardDeleteRepository<Grant, Guid>.HardDeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.HardDeleteByKeyAsync<Grant>([key], cancellationToken);

    void IHardDeleteRepository<Restriction, Guid>.HardDelete(Restriction aggregate) =>
        this.HardDelete(aggregate);

    Task IHardDeleteRepository<Restriction, Guid>.HardDeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.HardDeleteByKeyAsync<Restriction>([key], cancellationToken);

    void IHardDeleteRepository<Password, Guid>.HardDelete(Password aggregate) =>
        this.HardDelete(aggregate);

    Task IHardDeleteRepository<Password, Guid>.HardDeleteAsync(Guid key, CancellationToken cancellationToken) =>
        this.HardDeleteByKeyAsync<Password>([key], cancellationToken);

    void IHardDeleteRepository<WallOfShameEntry, Guid>.HardDelete(WallOfShameEntry aggregate) =>
        this.HardDelete(aggregate);

    Task IHardDeleteRepository<WallOfShameEntry, Guid>.HardDeleteAsync(
        Guid key,
        CancellationToken cancellationToken) =>
        this.HardDeleteByKeyAsync<WallOfShameEntry>([key], cancellationToken);
}
