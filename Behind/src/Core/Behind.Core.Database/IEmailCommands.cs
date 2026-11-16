namespace Behind.Core.Database;

public interface IEmailCommands
{
    public enum AddressStatus { Saved, NotFound, InUse, LastAddress }

    public sealed record AddressResult(AddressStatus Status, IEmailQueries.Address? Address = null);

    Task<AddressResult> SaveAddress(Guid userId, Guid? id, string value, bool confirmationRequired,
        CancellationToken cancellationToken = default);

    Task<AddressStatus> RemoveAddress(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<bool> ConfirmAddress(Guid userId, Guid id, string value, CancellationToken cancellationToken = default);

    Task Confirm(string email, CancellationToken cancellationToken = default);

    Task ConfirmPrimary(Guid userId, CancellationToken cancellationToken = default);

    Task SetConfirmed(Guid userId, bool confirmed, CancellationToken cancellationToken = default);

    Task ChangePrimary(Guid userId, string email, CancellationToken cancellationToken = default);
}