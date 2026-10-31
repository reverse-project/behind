using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public sealed class MinifyingFileProvider(IFileProvider inner, IReadOnlyList<IContentMinifier> minifiers) : IFileProvider
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly ConcurrentDictionary<string, Snapshot> _snapshots = new(StringComparer.Ordinal);

    public IFileInfo GetFileInfo(string subpath)
    {
        var file = inner.GetFileInfo(subpath);

        return file.Exists && !file.IsDirectory
            ? FindMinifier(file.Name).Match(() => file, minifier => Minified(subpath, file, minifier))
            : file;
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => inner.GetDirectoryContents(subpath);

    public IChangeToken Watch(string filter) => inner.Watch(filter);

    private Option<IContentMinifier> FindMinifier(string fileName) =>
        IsAlreadyMinified(fileName)
            ? None
            : minifiers.FirstOrDefault(minifier => minifier.Extensions.Contains(Path.GetExtension(fileName))) switch
            {
                { } found => Some(found),
                _ => None
            };

    private static bool IsAlreadyMinified(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName).EndsWith(".min", StringComparison.OrdinalIgnoreCase);

    private IFileInfo Minified(string subpath, IFileInfo file, IContentMinifier minifier)
    {
        if (_snapshots.TryGetValue(subpath, out var cached) && cached.IsFor(file))
        {
            return cached.File;
        }

        var snapshot = new Snapshot(file.LastModified, file.Length, Minify(file, minifier));
        _snapshots[subpath] = snapshot;

        return snapshot.File;
    }

    private static IFileInfo Minify(IFileInfo file, IContentMinifier minifier)
    {
        using var reader = new StreamReader(file.CreateReadStream(), Utf8);

        return minifier
            .Minify(reader.ReadToEnd())
            .Match(() => file, minified => new MinifiedFileInfo(file, Utf8.GetBytes(minified)));
    }

    private sealed record Snapshot(DateTimeOffset LastModified, long Length, IFileInfo File)
    {
        public bool IsFor(IFileInfo source) => LastModified == source.LastModified && Length == source.Length;
    }
}
