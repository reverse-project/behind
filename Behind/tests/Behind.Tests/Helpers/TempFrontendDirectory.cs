using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.Helpers;

public sealed class TempFrontendDirectory : IDisposable
{
    private readonly string _path = Directory.CreateTempSubdirectory("behind-frontend-").FullName;

    public PhysicalFileProvider Provider { get; }

    public TempFrontendDirectory(IReadOnlyDictionary<string, string> files)
    {
        foreach (var (name, content) in files)
        {
            Write(name, content);
        }

        Provider = new PhysicalFileProvider(_path);
    }

    public void Write(string name, string content, DateTime? lastWriteUtc = null)
    {
        var fullPath = Path.Combine(_path, name);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        if (lastWriteUtc is { } stamp)
        {
            File.SetLastWriteTimeUtc(fullPath, stamp);
        }
    }

    public void Dispose()
    {
        Provider.Dispose();
        Directory.Delete(_path, recursive: true);
    }
}
