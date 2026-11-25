using Behind.Common.Security;

namespace Behind.Tests;

public sealed class CertificateFilePermissionsTests
{
    [Fact]
    public void NewCredentialFileAndDirectoryAllowOnlyOwnerOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "secret.json");
        try
        {
            path.WriteNewFile([1, 2, 3]);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(directory));
            Assert.Single(Directory.GetFiles(directory));
            Assert.Throws<IOException>(() => path.WriteNewFile([4]));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void DirectCredentialWritesUsePrivatePermissionsAndDoNotCreateTemporaryFiles()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "secret.json");
        try
        {
            path.WriteFileDirect([1, 2, 3], false);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Single(Directory.GetFiles(directory));
            Assert.Throws<IOException>(() => path.WriteFileDirect([4], false));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
            path.WriteFileDirect([4], true);
            Assert.Equal(new byte[] { 4 }, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
