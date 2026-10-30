using System.CommandLine;
using Option = Behind.Common.Option<string>;
using System.Text.Json;
using Behind.Common.Security;
using Behind.Core.Common.Configuration;
using Behind.Core.Tools;

namespace Behind.Tests.Tools;

public sealed class DataProtectionSetupTests : IDisposable
{
    private const string Password = "pw-123";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"behind-tool-{Guid.NewGuid():N}");

    private string CertificatePath => Path.Combine(_root, "dp.pfx");

    private string OutputPath => Path.Combine(_root, "security.json");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private DataProtectionSetupRequest Request(Option output = default, bool force = false) =>
        new(CertificatePath, Path.Combine(_root, "keys"), Password, output, force);

    [Fact]
    public void CreateCertificate_CreatesLoadableCertificateAndReturnsAbsoluteSettings()
    {
        var settings = Request().CreateCertificate();

        using var certificate = CertificatePath.LoadCertificate(Password);
        Assert.True(certificate.HasPrivateKey);
        Assert.Equal(Path.GetFullPath(CertificatePath), settings.CertificatePath);
        Assert.Equal(Path.GetFullPath(Path.Combine(_root, "keys")), settings.KeysPath);
        Assert.Equal(Password, settings.CertificatePassword);
    }

    [Fact]
    public void CreateCertificate_ExistingWithoutForce_Throws()
    {
        Request().CreateCertificate();

        Assert.Throws<IOException>(() => Request().CreateCertificate());
    }

    [Fact]
    public void CreateCertificate_ExistingWithForce_Replaces()
    {
        Request().CreateCertificate();
        string first;
        using (var certificate = CertificatePath.LoadCertificate(Password))
        {
            first = certificate.Thumbprint;
        }

        Request(force: true).CreateCertificate();

        using var replaced = CertificatePath.LoadCertificate(Password);
        Assert.NotEqual(first, replaced.Thumbprint);
    }

    [Fact]
    public void ToSecurityJson_ProducesSecuritySectionRoundTrippable()
    {
        var settings = new DataProtectionSettings("/keys", "/cert.pfx", "pw");

        using var document = JsonDocument.Parse(settings.ToSecurityJson());
        var section = document.RootElement.GetProperty("Security").GetProperty("DataProtection");

        Assert.Equal("/keys", section.GetProperty("KeysPath").GetString());
        Assert.Equal("/cert.pfx", section.GetProperty("CertificatePath").GetString());
        Assert.Equal("pw", section.GetProperty("CertificatePassword").GetString());
    }

    [Fact]
    public void Run_WithoutOutputPath_WritesJsonToWriter()
    {
        var writer = new StringWriter();

        Request().Run(writer);

        Assert.Contains("\"DataProtection\"", writer.ToString());
        Assert.False(File.Exists(OutputPath));
    }

    [Fact]
    public void Run_WithOutputPath_WritesSecurityFile()
    {
        var writer = new StringWriter();

        Request(Some(OutputPath)).Run(writer);

        using var document = JsonDocument.Parse(File.ReadAllText(OutputPath));
        Assert.Equal(
            Password,
            document.RootElement.GetProperty("Security").GetProperty("DataProtection")
                .GetProperty("CertificatePassword").GetString()
        );
        Assert.Contains(OutputPath, writer.ToString());
    }

    [Fact]
    public void Run_ExistingOutputWithoutForce_Throws()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(OutputPath, "{}");

        Assert.Throws<IOException>(() => Request(Some(OutputPath)).Run(new StringWriter()));
    }

    [Fact]
    public void Run_ExistingOutputWithForce_Overwrites()
    {
        Request(Some(OutputPath)).Run(new StringWriter());

        Request(Some(OutputPath), true).Run(new StringWriter());

        Assert.Contains("DataProtection", File.ReadAllText(OutputPath));
    }

    [Fact]
    public void Command_Create_GeneratesRandomPasswordAndSecurityFile()
    {
        var root = new RootCommand().AddDataProtectionCommands();

        int exitCode = root.Parse([
            "dataprotection", "create",
            "--certificate", CertificatePath,
            "--keys-path", Path.Combine(_root, "keys"),
            "--output", OutputPath
        ]).Invoke();

        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(File.ReadAllText(OutputPath));
        string password = document.RootElement.GetProperty("Security").GetProperty("DataProtection")
            .GetProperty("CertificatePassword").GetString()!;
        Assert.Equal(32, password.Length);
        using var certificate = CertificatePath.LoadCertificate(password);
        Assert.True(certificate.HasPrivateKey);
    }
}
