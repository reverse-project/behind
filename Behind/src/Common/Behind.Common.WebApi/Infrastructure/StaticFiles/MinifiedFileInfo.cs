using Microsoft.Extensions.FileProviders;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public sealed record MinifiedFileInfo(IFileInfo Source, byte[] Content) : IFileInfo
{
    public bool Exists => true;

    public long Length => Content.Length;

    public string? PhysicalPath => null;

    public string Name => Source.Name;

    public DateTimeOffset LastModified => Source.LastModified;

    public bool IsDirectory => false;

    public Stream CreateReadStream() => new MemoryStream(Content, writable: false);
}
