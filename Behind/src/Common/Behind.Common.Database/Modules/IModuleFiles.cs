namespace Behind.Common.Database.Modules;

public interface IModuleFiles
{
    public const int MaxFileNameLength = 200;

    public sealed record Upload(string Type, string FileName, string ContentType, byte[] Content);

    public sealed record FileContent(string ContentType, byte[] Bytes);

    Task<Guid> Add(Upload upload, CancellationToken cancellationToken = default);

    Task Attach(
        string type,
        Guid parentId,
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken = default);

    Task<bool> Exists(string type, Guid key, CancellationToken cancellationToken = default);

    Task<Option<FileContent>> Open(string type, Guid key, CancellationToken cancellationToken = default);
}
