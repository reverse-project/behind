using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Common.Converters;
using Behind.Common.Logging;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Common.Security;
using Behind.Core.Common.Configuration;
using Behind.Core.Tools;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Settings;
using Behind.Forum.Common;
using Behind.MarkdownViewer.Common;
using Microsoft.Extensions.Configuration;
using static Behind.Common.FunctionalExtensions;

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
    private string CachingPath => Path.Combine(ConfigDirectory, "caching.json");

    private string FrontendPath => Path.Combine(ConfigDirectory, "frontend.json");

    private string EmailPath => Path.Combine(ConfigDirectory, "email.json");

    private string AppSettingsPath => Path.Combine(ConfigDirectory, "appsettings.json");

    private string ForumPath => Path.Combine(ConfigDirectory, "forum.json");

    private string MarkdownViewerPath => Path.Combine(ConfigDirectory, "markdownviewer.json");

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

    private static IConfigurationRoot Load(string path) =>
        new ConfigurationBuilder().AddJsonFile(path).Build();

    private static T Bind<T>(string path, string section) =>
        Load(path).GetSection(section).Get<T>()!;

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    [Fact]
    public void Run_CreatesAllConfigurationFilesAndCertificate()
    {
        Request().Run(new StringWriter());

        Assert.True(File.Exists(SecurityPath));
        Assert.True(File.Exists(DatabasePath));
        Assert.True(File.Exists(BackendPath));
        Assert.True(File.Exists(CachingPath));
        Assert.True(File.Exists(FrontendPath));
        Assert.True(File.Exists(EmailPath));
        Assert.True(File.Exists(AppSettingsPath));
        Assert.True(File.Exists(ForumPath));
        Assert.True(File.Exists(MarkdownViewerPath));
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
    public void Run_FrontendFileHasEditableCarouselSettings()
    {
        Request().Run(new StringWriter());

        var carousels = Section(FrontendPath, "Frontend").GetProperty("Carousels");
        Assert.Equal(CarouselsConfiguration.DefaultMaxSlides, carousels.GetProperty("MaxSlides").GetInt32());
        Assert.Equal(CarouselsConfiguration.DefaultMinIntervalSeconds, carousels.GetProperty("MinIntervalSeconds").GetInt32());
        Assert.Equal(CarouselsConfiguration.DefaultMaxIntervalSeconds, carousels.GetProperty("MaxIntervalSeconds").GetInt32());
        Assert.Equal(CarouselsConfiguration.DefaultIntervalSeconds, carousels.GetProperty("IntervalSeconds").GetInt32());
        Assert.Equal(CarouselsConfiguration.DefaultMaxCaptionLength, carousels.GetProperty("MaxCaptionLength").GetInt32());
        Assert.Equal(CarouselsConfiguration.DefaultMaxImageBytes, carousels.GetProperty("MaxImageBytes").GetInt64());
        Assert.Equal(ImageThumbnailExtensions.MaxPixels, Section(FrontendPath, "Frontend").GetProperty("Galleries").GetProperty("MaxDecodedPixels").GetInt64());
    }

    [Fact]
    public void Run_GeneratesEveryConfigurationFileOfTheWebHostAndItsModules()
    {
        Request().Run(new StringWriter());

        string[] expected =
        [
            ..ConfigurationFileCatalog.DefaultNames
                .Concat(new[]
                    {
                        CoreModule.Descriptor,
                        Behind.Contact.WebApi.ContactModule.Descriptor,
                        Behind.Forum.WebApi.ForumModule.Descriptor,
                        Behind.MarkdownViewer.WebApi.MarkdownViewerModule.Descriptor,
                        Behind.Quotes.WebApi.QuotesModule.Descriptor,
                        Behind.Sport.WebApi.SportModule.Descriptor
                    }
                    .SelectMany(module => module.ConfigurationFiles))
                .Distinct()
                .Order()
        ];

        Assert.Equal(expected, Directory.GetFiles(ConfigDirectory).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Run_BackendFileBindsToTheDefaultsOfEveryBackendSection()
    {
        Request().Run(new StringWriter());

        Assert.Equal(new ContentRestrictionsConfiguration(), Bind<ContentRestrictionsConfiguration>(BackendPath, ContentRestrictionsConfiguration.SectionName));
        Assert.Equal(new DownloadsConfiguration(), Bind<DownloadsConfiguration>(BackendPath, DownloadsConfiguration.SectionName));
        Assert.Equal(new CommentsConfiguration(), Bind<CommentsConfiguration>(BackendPath, CommentsConfiguration.SectionName));
        Assert.Equal(new AccountsConfiguration(), Bind<AccountsConfiguration>(BackendPath, AccountsConfiguration.SectionName));
        Assert.Equal(new SchedulerConfiguration(), Bind<SchedulerConfiguration>(BackendPath, SchedulerConfiguration.SectionName));
        var forwarded = Bind<ForwardedHeadersConfiguration>(BackendPath, ForwardedHeadersConfiguration.SectionName);
        Assert.False(forwarded.Enabled);
        Assert.Empty(forwarded.KnownForwarders);
        Assert.Equal(new ForwardedHeadersConfiguration().ForwardLimit, forwarded.ForwardLimit);
        var backend = Section(BackendPath, "Backend");
        Assert.Empty(backend.GetProperty("ForwardedHeaders").GetProperty("KnownForwarders").EnumerateArray());
        foreach (var name in new[] { "ContentRestrictions", "Downloads", "Comments", "Accounts", "Scheduler" })
        {
            Assert.NotEmpty(backend.GetProperty(name).EnumerateObject());
        }
    }

    [Fact]
    public void Run_FrontendFileBindsToTheDefaultsOfEveryFrontendSection()
    {
        Request().Run(new StringWriter());

        Assert.Equal(new SitesConfiguration { IndexSlug = string.Empty }, Bind<SitesConfiguration>(FrontendPath, SitesConfiguration.SectionName));
        Assert.Equal(new GalleriesConfiguration(), Bind<GalleriesConfiguration>(FrontendPath, GalleriesConfiguration.SectionName));
        Assert.Equal(new CarouselsConfiguration(), Bind<CarouselsConfiguration>(FrontendPath, CarouselsConfiguration.SectionName));
        Assert.Equal(Json(FrontendOptions.Default), Json(Bind<FrontendOptions>(FrontendPath, FrontendOptions.SectionName)));
        var staticFiles = JsonNode.Parse(FrontendOptions.Default.ToConfigurationJson())!.AsObject();
        staticFiles.Remove(nameof(FrontendOptions.CompressedMimeTypes));
        Assert.True(JsonNode.DeepEquals(staticFiles, JsonNode.Parse(File.ReadAllText(FrontendPath))!["Frontend"]!["StaticFiles"]));
        Assert.Equal(StartupSetupRequest.DefaultFrontendUrl, Load(FrontendPath)["Frontend:Url"]);
    }

    [Fact]
    public void Run_SecurityFileHasEveryMagicLinkAndOidcDefault()
    {
        Request().Run(new StringWriter());

        OptionTypeConverterRegistration.Register<SecretKey>();
        OptionTypeConverterRegistration.Register<JwtSettings>();
        OptionTypeConverterRegistration.Register<MagicLinkSettings>();
        OptionTypeConverterRegistration.Register<DataProtectionSettings>();
        OptionTypeConverterRegistration.Register<ElevationSettings>();
        var security = Load(SecurityPath)
            .GetSection(SecurityConfiguration.SectionName)
            .GetOptionAware<SecurityConfiguration>()!;
        var magicLink = security.MagicLink.Match(() => null!, link => link);

        Assert.False(security.ReadOnlyConfiguration);
        Assert.False(security.TwoFactorRequired);
        Assert.Equal(SecurityConfiguration.DefaultSignInNotificationTimeoutMilliseconds, security.SignInNotificationTimeoutMilliseconds);
        Assert.Equal(SecurityConfiguration.DefaultSignInNotificationTimeoutMilliseconds,
            Load(SecurityPath).GetValue<int>("Security:SignInNotificationTimeoutMilliseconds"));
        Assert.True(security.Accounts.Subaccounts);
        Assert.False(security.Accounts.LinkUnlink);
        Assert.Equal(SecurityAccountSettings.DefaultSwitchPinMaxLength, security.Accounts.SwitchPinMaxLength);
        Assert.Equal(SecurityAccountSettings.DefaultSwitchPinMaxLength, Load(SecurityPath).GetValue<int>("Security:Accounts:SwitchPinMaxLength"));
        Assert.Equal("True", Load(SecurityPath)["Security:Accounts:Subaccounts"], ignoreCase: true);
        Assert.Equal("False", Load(SecurityPath)["Security:Accounts:LinkUnlink"], ignoreCase: true);
        Assert.Equal(Some(MagicLinkSettings.DefaultRotationInterval), magicLink.RotationInterval);
        Assert.Equal(Some(MagicLinkSettings.DefaultRotationOverlap), magicLink.RotationOverlap);
        Assert.Equal(Some(MagicLinkSettings.DefaultLinkLifetime), magicLink.LinkLifetime);
        Assert.Equal(Some(MagicLinkSettings.DefaultRequestCooldown), magicLink.RequestCooldown);
        Assert.Equal(Some(MagicLinkSettings.DefaultRequestLimit), magicLink.RequestLimit);
        Assert.Equal(Some(MagicLinkSettings.DefaultRequestWindow), magicLink.RequestWindow);
        Assert.Equal(Some(MagicLinkSettings.DefaultRedeemLimit), magicLink.RedeemLimit);
        Assert.Equal(Some(MagicLinkSettings.DefaultRedeemWindow), magicLink.RedeemWindow);
        Assert.Equal(None, magicLink.OldSecret);
        Assert.Equal(Json(new OidcConfiguration()), Json(Bind<OidcConfiguration>(SecurityPath, OidcConfiguration.SectionName)));
    }

    [Fact]
    public void Run_CachingFileHasOnlyProviderAndKeyAndAnEmptyNamedConnection()
    {
        Request().Run(new StringWriter());
        OptionTypeConverterRegistration.Register<CachingKey>();
        var configuration = Load(CachingPath);
        var caching = configuration.GetSection(CachingConfiguration.SectionName)
            .GetOptionAware<CachingConfiguration>()!;
        Assert.IsType<Memory>(caching.Provider);
        Assert.Equal("CachingConnection", caching.Key.Match(() => string.Empty, key => key.Value));
        Assert.Equal(string.Empty, configuration.GetConnectionString("CachingConnection"));
        Assert.Equal(new[] { "Key", "Provider" }, Section(CachingPath, "Caching")
            .EnumerateObject().Select(property => property.Name).Order());
        Assert.Null(Load(BackendPath)["Backend:Caching:Provider"]);
    }

    [Fact]
    public void Run_EmailFileUsesTheLoggerProvider()
    {
        Request().Run(new StringWriter());

        Assert.Equal(LoggerEmailConfiguration.ProviderName, Load(EmailPath)["Email:Provider"]);
    }

    [Fact]
    public void Run_AppSettingsFileHasTheLoggingDefaultsAndADisabledOpenTelemetry()
    {
        Request().Run(new StringWriter());

        using var defaults = typeof(SerilogExtensions).Assembly.GetManifestResourceStream(SerilogExtensions.DefaultsResourceName)!;
        var appSettings = JsonNode.Parse(File.ReadAllText(AppSettingsPath))!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(defaults)!["Serilog"], appSettings["Serilog"]));
        Assert.Equal("*", appSettings["AllowedHosts"]!.GetValue<string>());
        var openTelemetry = Load(AppSettingsPath)
            .GetSection(OpenTelemetryConfiguration.SectionName)
            .Get<OpenTelemetryConfiguration>(options => options.ErrorOnUnknownConfiguration = true)!;
        Assert.False(openTelemetry.IsEnabled);
        Assert.Equal(StartupSetupRequest.DefaultServiceName, openTelemetry.ServiceName);
        Assert.False(openTelemetry.Traces!.IsEnabled);
        Assert.False(openTelemetry.Metrics!.IsEnabled);
        Assert.False(openTelemetry.Logging!.IsEnabled);
    }

    [Fact]
    public void Run_ModuleFilesBindToTheModuleDefaults()
    {
        Request().Run(new StringWriter());

        Assert.Equal(new ForumSettings(), Bind<ForumSettings>(ForumPath, ForumSettings.SectionName));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(new ForumSettings().ToConfigurationJson()),
            JsonNode.Parse(File.ReadAllText(ForumPath))!["Forum"]));
        var markdownViewer = Bind<MarkdownViewerConfiguration>(MarkdownViewerPath, MarkdownViewerConfiguration.SectionName);
        Assert.Equal(MarkdownViewerConfiguration.DefaultTitle, markdownViewer.Title);
        Assert.Equal(MarkdownViewerConfiguration.DefaultDocumentName, markdownViewer.DefaultDocument);
        Assert.Equal(Path.GetFullPath(Path.Combine(DataDirectory, "docs")), markdownViewer.SourceDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(DataDirectory, "docs-site")), markdownViewer.OutputDirectory);
    }

    [Fact]
    public void Command_Create_WritesTheFrontendUrl()
    {
        var root = new RootCommand().AddStartupCommands();

        int exitCode = root.Parse([
            "startup", "create",
            "--data-directory", DataDirectory,
            "--config-directory", ConfigDirectory,
            "--frontend-url", "https://example.com/"
        ]).Invoke();

        Assert.Equal(0, exitCode);
        Assert.Equal("https://example.com/", Load(FrontendPath)["Frontend:Url"]);
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
        Assert.Contains(AppSettingsPath, text);
        Assert.Contains(MarkdownViewerPath, text);
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
    public void Run_ForceWritesExistingFilesInPlace()
    {
        Request().Run(new StringWriter());
        using var security = new FileStream(SecurityPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var certificate = new FileStream(CertificatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        string original = File.ReadAllText(SecurityPath);

        Request(force: true).Run(new StringWriter());

        using var reader = new StreamReader(security);
        Assert.Equal(File.ReadAllText(SecurityPath), reader.ReadToEnd());
        Assert.NotEqual(original, File.ReadAllText(SecurityPath));
        using var copiedCertificate = new MemoryStream();
        certificate.CopyTo(copiedCertificate);
        Assert.Equal(File.ReadAllBytes(CertificatePath), copiedCertificate.ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Run_DirectoryAtFilePathFailsBeforeGeneratingAnything(bool force, bool skipExisting)
    {
        Directory.CreateDirectory(BackendPath);
        File.WriteAllText(Path.Combine(BackendPath, "preserved"), "keep");

        var exception = Assert.Throws<IOException>(() => Request(force, skipExisting).Run(new StringWriter()));

        Assert.Contains(BackendPath, exception.Message);
        Assert.False(File.Exists(SecurityPath));
        Assert.False(File.Exists(CertificatePath));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(BackendPath, "preserved")));
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
