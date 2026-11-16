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

    // Runs the account creation and links the identity in one transaction, so a failed link also
    // removes the new account. Returns false and rolls back when create reports a failure.
    Task<bool> CreateAndLink(
        Func<Task<bool>> create,
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
