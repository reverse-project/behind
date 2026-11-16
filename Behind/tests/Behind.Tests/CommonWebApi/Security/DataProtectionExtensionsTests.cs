using System.Security.Cryptography;
using Behind.Common.Security;
using Behind.Common.WebApi.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CommonWebApi.Security;

public sealed class DataProtectionExtensionsTests : IDisposable
{
    private const string Password = "test-password";
    private const string Application = "Behind.Tests";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"behind-dp-{Guid.NewGuid():N}");

    private string KeysPath => Path.Combine(_root, "keys");

    private string CertificatePath => Path.Combine(_root, "dp.pfx");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private IDataProtector NewProtector(string password = Password)
    {
        var services = new ServiceCollection();
        services.AddCertificateProtectedDataProtection(Application, KeysPath, CertificatePath, password);
        return services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("purpose");
    }

    [Fact]
    public void LoadOrCreateCertificate_MissingFile_CreatesCertificateWithPrivateKey()
    {
        using var certificate = CertificatePath.LoadOrCreateCertificate(Password);

        Assert.True(File.Exists(CertificatePath));
        Assert.True(certificate.HasPrivateKey);
    }

    [Fact]
    public void LoadOrCreateCertificate_ExistingFile_ReturnsSameCertificate()
    {
        using var first = CertificatePath.LoadOrCreateCertificate(Password);
        using var second = CertificatePath.LoadOrCreateCertificate(Password);

        Assert.Equal(first.Thumbprint, second.Thumbprint);
    }

    [Fact]
    public void LoadOrCreateCertificate_WrongPassword_Throws()
    {
        CertificatePath.LoadOrCreateCertificate(Password).Dispose();

        Assert.ThrowsAny<CryptographicException>(() => CertificatePath.LoadOrCreateCertificate("wrong"));
    }

    [Fact]
    public void LoadOrCreateCertificate_LeavesNoTemporaryFiles()
    {
        CertificatePath.LoadOrCreateCertificate(Password).Dispose();

        Assert.Single(Directory.GetFiles(_root));
    }

        [Fact]
    public void Protect_AcrossRestarts_UnprotectsWithPersistedKey()
    {
        string payload = NewProtector().Protect("secret");

        Assert.Equal("secret", NewProtector().Unprotect(payload));
    }

    [Fact]
    public void Protect_KeysAreEncryptedAtRest()
    {
        NewProtector().Protect("secret");

        string[] keyContents = Directory.GetFiles(KeysPath, "*.xml").Select(File.ReadAllText).ToArray();

        Assert.NotEmpty(keyContents);
        Assert.All(keyContents, content =>
        {
            Assert.Contains("EncryptedData", content);
            Assert.DoesNotContain("<masterKey", content);
        });
    }

    [Fact]
    public void Protect_PayloadFromDifferentApplication_CannotBeUnprotected()
    {
        string payload = NewProtector().Protect("secret");

        var other = new ServiceCollection();
        other.AddCertificateProtectedDataProtection("Other", KeysPath, CertificatePath, Password);
        var protector = other.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("purpose");

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(payload));
    }
}
