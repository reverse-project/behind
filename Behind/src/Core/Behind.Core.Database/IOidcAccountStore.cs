namespace Behind.Core.Database;

public interface IOidcAccountStore
{
    Task<Guid?> FindUser(
        string providerId,
        string issuer,
        string subject,
        CancellationToken cancellationToken);

    Task Link(
        Guid userId,
        string providerId,
        string issuer,
        string subject,
        CancellationToken cancellationToken);

    Task SynchronizeRoles(
        Guid userId,
        string providerId,
        IReadOnlyList<string> mappedRoleNames,
        IReadOnlyList<string> autoRoleNames,
        CancellationToken cancellationToken);
}
