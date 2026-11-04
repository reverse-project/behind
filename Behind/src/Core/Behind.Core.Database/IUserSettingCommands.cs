namespace Behind.Core.Database;

public interface IUserSettingCommands
{
    Task Set(Guid userId, string key, string value, CancellationToken cancellationToken = default);
}
