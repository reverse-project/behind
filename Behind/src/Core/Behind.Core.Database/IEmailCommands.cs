namespace Behind.Core.Database;

public interface IEmailCommands
{
    Task Confirm(string email, CancellationToken cancellationToken = default);
}