namespace Behind.Core.Database;

public interface IUserSettingQueries
{
    Task<IReadOnlyDictionary<string, string>> GetAll(Guid userId, CancellationToken cancellationToken = default);
}
