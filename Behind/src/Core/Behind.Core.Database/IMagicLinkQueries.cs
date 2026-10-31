namespace Behind.Core.Database;

public interface IMagicLinkQueries
{
    Task<bool> HasIssuedSince(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default);
}
