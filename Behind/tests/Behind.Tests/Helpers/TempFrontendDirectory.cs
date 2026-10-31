using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.Helpers;

public sealed class TempFrontendDirectory : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("behind-frontend-").FullName;

    public PhysicalFileProvider Provider { get; }

    public TempFrontendDirectory(IReadOnlyDictionary<string, string> files)
    {
        foreach (var (name, content) in files)
        {
            Write(name, content);
        }

        Provider = new PhysicalFileProvider(Root);
    }

    public void Write(string name, string content, DateTime? lastWriteUtc = null) =>
        Write(name, File.WriteAllText, content, lastWriteUtc);

    public void WriteBytes(string name, byte[] content, DateTime? lastWriteUtc = null) =>
        Write(name, File.WriteAllBytes, content, lastWriteUtc);

    private void Write<T>(string name, Action<string, T> write, T content, DateTime? lastWriteUtc)
    {
        var fullPath = Path.Combine(Root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        write(fullPath, content);

        if (lastWriteUtc is { } stamp)
        {
            File.SetLastWriteTimeUtc(fullPath, stamp);
        }
    }

    public void Dispose()
    {
        Provider.Dispose();
        Directory.Delete(Root, recursive: true);
    }
}
