namespace Behind.Common.Storage;

public interface IContentStore
{
    Task Write(string key, byte[] content, CancellationToken cancellationToken = default);

    Task<Option<byte[]>> Read(string key, CancellationToken cancellationToken = default);

    Task Delete(string key, CancellationToken cancellationToken = default);

    Task Move(string key, string newKey, CancellationToken cancellationToken = default);
}
