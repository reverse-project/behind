using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Storage;

public sealed class DirectoryContentStore : IContentStore
{
    private readonly string _root;

    public DirectoryContentStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    public async Task Write(string key, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = Resolve(key);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            await File.WriteAllBytesAsync(temporary, content, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public async Task<Option<byte[]>> Read(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return Some(await File.ReadAllBytesAsync(Resolve(key), cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return None;
        }
    }

    public Task Delete(string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task Move(string key, string newKey, CancellationToken cancellationToken = default)
    {
        var source = Resolve(key);
        var target = Resolve(newKey);

        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target, overwrite: true);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = Path.GetFullPath(Path.Combine(_root, key));

        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException("The key must stay inside the content directory.", nameof(key));
    }
}
