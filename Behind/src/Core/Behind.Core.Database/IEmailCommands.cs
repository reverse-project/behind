namespace Behind.Core.Database;

public interface IEmailCommands
{
    Task Confirm(string email, CancellationToken cancellationToken = default);

    Task SetConfirmed(Guid userId, bool confirmed, CancellationToken cancellationToken = default);

    Task ChangePrimary(Guid userId, string email, CancellationToken cancellationToken = default);
}