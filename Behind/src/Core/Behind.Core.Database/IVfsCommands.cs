namespace Behind.Core.Database;

public interface IVfsCommands
{
    public sealed record Upload(string Type, string FileName, string ContentType, byte[] Content);

    Task<Guid> Add(Upload upload, CancellationToken cancellationToken = default);

    Task Attach(
        string type,
        Guid parentId,
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken = default);

    Task Delete(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken = default);
}
