using System.CommandLine;
using System.Text.Json;
using Behind.Common.Configuration;
using Behind.Common.Converters;
using Behind.Common.Security;
using Behind.Core.Common.Configuration;
using Behind.Core.Tools;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.Tools;

public sealed class StartupSetupTests : IDisposable
{
    private const string Password = "pw-123";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"behind-startup-{Guid.NewGuid():N}");

    private string DataDirectory => Path.Combine(_root, "data");

    private string ConfigDirectory => Path.Combine(_root, "config");

    private string SecurityPath => Path.Combine(ConfigDirectory, "security.json");

    private string DatabasePath => Path.Combine(ConfigDirectory, "database.json");

    private string BackendPath => Path.Combine(ConfigDirectory, "backend.json");

    private string FrontendPath => Path.Combine(ConfigDirectory, "frontend.json");

    private string CertificatePath => Path.Combine(DataDirectory, "dataprotection.pfx");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private StartupSetupRequest Request(bool force = false, bool skipExisting = false, bool emailConfirmation = false) =>
        new(
            DataDirectory,
            ConfigDirectory,
            StartupSetupRequest.DefaultIssuer,
            StartupSetupRequest.DefaultAudience,
            emailConfirmation,
            Password,
            force,
            skipExisting);

    private static JsonElement Section(string path, string name) =>
        JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty(name);

    [Fact]
    public void Run_CreatesAllConfigurationFilesAndCertificate()
    {
        Request().Run(new StringWriter());

        Assert.True(File.Exists(SecurityPath));
        Assert.True(File.Exists(DatabasePath));
        Assert.True(File.Exists(BackendPath));
        Assert.True(File.Exists(FrontendPath));
        using var certificate = CertificatePath.LoadCertificate(Password);
        Assert.True(certificate.HasPrivateKey);
        Assert.True(Directory.Exists(Path.Combine(DataDirectory, "DataProtection-Keys")));
    }

    [Fact]
    public void Run_SecurityFileIsLoadableAsSecurityConfigurationWithAllSecrets()
    {
        Request(emailConfirmation: true).Run(new StringWriter());

        OptionTypeConverterRegistration.Register<SecretKey>();
        OptionTypeConverterRegistration.Register<JwtSettings>();
        OptionTypeConverterRegistration.Register<MagicLinkSettings>();
        OptionTypeConverterRegistration.Register<DataProtectionSettings>();
        OptionTypeConverterRegistration.Register<ElevationSettings>();
        var security = new ConfigurationBuilder()
            .AddJsonFile(SecurityPath)
            .Build()
            .GetSection(SecurityConfiguration.SectionName)
            .GetOptionAware<SecurityConfiguration>()!;

        Assert.True(security.EmailConfirmation);
        Assert.True(security.IsElevationEnabled);
        Assert.IsType<Totp>(security.SecretKey.Match(() => null!, key => key));
        Assert.IsType<Totp>(security.DeveloperElevationSecretKey.Match(() => null!, key => key));
        Assert.Equal(
            JwtSettings.GeneratedSigningKeyLength,
            security.Jwt.Match(() => null!, jwt => jwt).SigningKey.Length);
        Assert.Equal(StartupSetupRequest.DefaultIssuer, security.Jwt.Match(() => null!, jwt => jwt).Issuer);
        Assert.Equal(StartupSetupRequest.DefaultAudience, security.Jwt.Match(() => null!, jwt => jwt).Audience);
        Assert.NotEqual(Guid.Empty, security.MagicLink.Match(() => null!, link => link).ActualSecret.Value);
        var dataProtection = security.DataProtection.Match(() => null!, settings => settings);
        Assert.Equal(Path.GetFullPath(CertificatePath), dataProtection.CertificatePath);
        Assert.Equal(Path.GetFullPath(Path.Combine(DataDirectory, "DataProtection-Keys")), dataProtection.KeysPath);
        Assert.Equal(Password, dataProtection.CertificatePassword);
    }

    [Fact]
    public void Run_SecretsAreDifferentOnEveryRun()
    {
        Request().Run(new StringWriter());
        string first = File.ReadAllText(SecurityPath);

        Request(force: true).Run(new StringWriter());

        Assert.NotEqual(first, File.ReadAllText(SecurityPath));
    }

    [Fact]
    public void Run_DatabaseFileUsesSqliteInTheDataDirectory()
    {
        Request().Run(new StringWriter());

        var database = Section(DatabasePath, "Database");
        Assert.Equal("Sqlite", database.GetProperty("Provider").GetString());
        Assert.Equal("DefaultConnection", database.GetProperty("Key").GetString());
        Assert.Equal(
            $"Data Source={Path.GetFullPath(Path.Combine(DataDirectory, "web.db"))}",
            Section(DatabasePath, "ConnectionStrings").GetProperty("DefaultConnection").GetString());
    }

    [Fact]
    public void Run_BackendFilePointsToTheDataDirectory()
    {
        Request().Run(new StringWriter());

        Assert.Equal(
            Path.GetFullPath(DataDirectory),
            Section(BackendPath, "Backend").GetProperty("DataDirectory").GetString());
    }

    [Fact]
    public void Run_FrontendFileHasEditableSiteAndSitesSettings()
    {
        Request().Run(new StringWriter());

        var frontend = Section(FrontendPath, "Frontend");
        Assert.Equal("Behind", frontend.GetProperty("Site").GetProperty("Title").GetString());
        Assert.Equal("en-US", frontend.GetProperty("Site").GetProperty("DefaultLocale").GetString());
        Assert.Equal(
            SitesConfiguration.DefaultMaxImageBytes,
            frontend.GetProperty("Sites").GetProperty("MaxImageBytes").GetInt64());
    }

    [Fact]
    public void Run_PrintsFilesAndAuthenticatorUris()
    {
        var writer = new StringWriter();

        Request().Run(writer);

        string text = writer.ToString();
        Assert.Contains(SecurityPath, text);
        Assert.Contains(DatabasePath, text);
        Assert.Contains(BackendPath, text);
        Assert.Contains("otpauth://totp/", text);
        Assert.Contains(":developer?", text);
    }

    [Fact]
    public void Run_ExistingFilesWithoutForce_ThrowsAndWritesNothingNew()
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(BackendPath, "{}");

        Assert.Throws<IOException>(() => Request().Run(new StringWriter()));

        Assert.False(File.Exists(SecurityPath));
        Assert.False(File.Exists(CertificatePath));
        Assert.False(File.Exists(DatabasePath));
        Assert.Equal("{}", File.ReadAllText(BackendPath));
    }

    [Fact]
    public void Run_ExistingFilesWithForce_Replaces()
    {
        Request().Run(new StringWriter());
        string first = File.ReadAllText(SecurityPath);

        Request(force: true).Run(new StringWriter());

        Assert.NotEqual(first, File.ReadAllText(SecurityPath));
    }

    [Fact]
    public void Run_ExistingFilesWithSkipExisting_KeepsThemAndSucceeds()
    {
        Request().Run(new StringWriter());
        string security = File.ReadAllText(SecurityPath);
        var writer = new StringWriter();

        Request(skipExisting: true).Run(writer);

        Assert.Equal(security, File.ReadAllText(SecurityPath));
        Assert.Contains("Skipped", writer.ToString());
    }

    [Fact]
    public void Run_SkipExistingGeneratesOnlyTheMissingFiles()
    {
        Request().Run(new StringWriter());
        File.Delete(DatabasePath);
        string security = File.ReadAllText(SecurityPath);

        Request(skipExisting: true).Run(new StringWriter());

        Assert.True(File.Exists(DatabasePath));
        Assert.Equal(security, File.ReadAllText(SecurityPath));
    }

    [Fact]
    public void Run_SkipExistingWithOnlyTheCertificatePresent_Throws()
    {
        Request().Run(new StringWriter());
        File.Delete(SecurityPath);

        Assert.Throws<IOException>(() => Request(skipExisting: true).Run(new StringWriter()));
    }

    [Fact]
    public void Command_Create_GeneratesFilesWithRandomPassword()
    {
        var root = new RootCommand().AddStartupCommands();

        int exitCode = root.Parse([
            "startup", "create",
            "--data-directory", DataDirectory,
            "--config-directory", ConfigDirectory,
            "--email-confirmation"
        ]).Invoke();

        Assert.Equal(0, exitCode);
        var security = Section(SecurityPath, "Security");
        string password = security.GetProperty("DataProtection").GetProperty("CertificatePassword").GetString()!;
        Assert.Equal(32, password.Length);
        Assert.True(security.GetProperty("EmailConfirmation").GetBoolean());
        using var certificate = CertificatePath.LoadCertificate(password);
        Assert.True(certificate.HasPrivateKey);
    }

    [Fact]
    public void Command_Create_SecondRunWithSkipExistingSucceeds()
    {
        var root = new RootCommand().AddStartupCommands();
        string[] arguments =
        [
            "startup", "create",
            "--data-directory", DataDirectory,
            "--config-directory", ConfigDirectory,
            "--skip-existing"
        ];

        Assert.Equal(0, root.Parse(arguments).Invoke());
        Assert.Equal(0, root.Parse(arguments).Invoke());
    }
}
