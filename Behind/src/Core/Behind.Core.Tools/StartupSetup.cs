using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Tools;

public sealed record StartupSetupRequest(
    string DataDirectory,
    string ConfigDirectory,
    string Issuer,
    string Audience,
    bool EmailConfirmation,
    string Password,
    bool Force,
    bool SkipExisting,
    string FrontendUrl = StartupSetupRequest.DefaultFrontendUrl
)
{
    public const string AppSettingsFileName = "appsettings.json";
    public const string SecurityFileName = "security.json";
    public const string DatabaseFileName = "database.json";
    public const string BackendFileName = "backend.json";
    public const string CachingFileName = "caching.json";
    public const string FrontendFileName = "frontend.json";
    public const string EmailFileName = "email.json";
    public const string ForumFileName = "forum.json";
    public const string MarkdownViewerFileName = "markdownviewer.json";
    public const string CertificateFileName = "dataprotection.pfx";
    public const string KeysDirectoryName = "DataProtection-Keys";
    public const string DatabaseFileNameInData = "web.db";
    public const string DocsDirectoryName = "docs";
    public const string DocsSiteDirectoryName = "docs-site";
    public const string ConnectionStringName = "DefaultConnection";
    public const string DefaultIssuer = "Behind Backend";
    public const string DefaultAudience = "Behind Frontend";
    public const string DefaultTitle = "Behind";
    public const string DefaultLocale = "en-US";
    public const string DefaultFrontendUrl = "http://localhost:8080/";
    public const string DefaultServiceName = "Behind";
    public const string DefaultOtlpEndpoint = "http://localhost:4317";
    public const string DefaultAllowedHosts = "*";
    public const string SerilogSectionName = "Serilog";
    public const string AdminAccount = "admin";
    public const string DeveloperAccount = "developer";
}

public enum StartupArtifactAction
{
    Generate,
    Skip,
    Replace,
    Conflict
}

public sealed record StartupArtifact(
    string ConfigurationPath,
    IReadOnlyList<string> OwnedPaths,
    Func<StartupRendering> Render
);

public sealed record StartupRendering(string Content, IReadOnlyList<string> Messages);

public sealed record StartupPlanStep(StartupArtifact Artifact, StartupArtifactAction Action);

public sealed record StartupSecurityFile(StartupSecuritySection Security);

public sealed record StartupSecuritySection(
    bool ReadOnlyConfiguration,
    bool EmailConfirmation,
    bool TwoFactorRequired,
    DataProtectionSettings DataProtection,
    SecretKey SecretKey,
    JwtSettings Jwt,
    ElevationSettings Elevation,
    StartupMagicLinkSettings MagicLink,
    OidcConfiguration Oidc
);

public sealed record StartupMagicLinkSettings(
    SecretKey ActualSecret,
    TimeSpan RotationInterval,
    TimeSpan RotationOverlap,
    TimeSpan LinkLifetime,
    TimeSpan RequestCooldown,
    int RequestLimit,
    TimeSpan RequestWindow,
    int RedeemLimit,
    TimeSpan RedeemWindow
);

public sealed record StartupDatabaseFile(
    IReadOnlyDictionary<string, string> ConnectionStrings,
    DatabaseConfiguration Database
);

public sealed record StartupBackendFile(StartupBackendSection Backend);

public sealed record StartupCachingFile(
    IReadOnlyDictionary<string, string> ConnectionStrings,
    CachingConfiguration Caching);

public sealed record StartupBackendSection(
    string DataDirectory,
    ContentRestrictionsConfiguration ContentRestrictions,
    DownloadsConfiguration Downloads,
    CommentsConfiguration Comments,
    AccountsConfiguration Accounts,
    SchedulerConfiguration Scheduler
)
{
    public int AuditSearchMaxEntries { get; init; } = new BackendConfiguration().AuditSearchMaxEntries;

    public int AuditSearchMaxCandidates { get; init; } = new BackendConfiguration().AuditSearchMaxCandidates;
}

public sealed record StartupFrontendFile(StartupFrontendSection Frontend);

public sealed record StartupFrontendSection(
    string Url,
    StartupSiteSection Site,
    SitesConfiguration Sites,
    GalleriesConfiguration Galleries,
    CarouselsConfiguration Carousels,
    StartupStaticFilesSection StaticFiles
);

public sealed record StartupSiteSection(string Title, string Copyright, string DefaultLocale);

// Mirrors Behind.Common.WebApi FrontendOptions; StartupSetupTests guards that the values stay identical.
// CompressedMimeTypes is left out: the configuration binder appends list items to the initialized defaults.
public sealed record StartupStaticFilesSection
{
    public bool Minify { get; init; } = true;

    public bool Compress { get; init; } = true;

    public string DefaultFile { get; init; } = "index.html";

    public string CacheControl { get; init; } = "no-cache";

    public string ContentSecurityPolicy { get; init; } =
        "default-src 'self'; script-src 'self' 'unsafe-eval'; style-src 'self'; img-src 'self' data:; " +
        "connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    public string ReferrerPolicy { get; init; } = "no-referrer";
}

public sealed record StartupEmailFile(EmailConfiguration Email);

public sealed record StartupAppSettingsFile(
    JsonNode? Serilog,
    StartupOpenTelemetrySection OpenTelemetry,
    string AllowedHosts
);

public sealed record StartupOpenTelemetrySection(
    string ServiceName,
    bool IsEnabled,
    StartupOtlpExporterSection Traces,
    StartupOtlpExporterSection Metrics,
    StartupOtlpLoggingSection Logging
);

public sealed record StartupOtlpExporterSection(
    bool IsEnabled,
    string Endpoint,
    string Protocol,
    IReadOnlyList<string> Instruments
);

public sealed record StartupOtlpLoggingSection(
    bool IsEnabled,
    string Endpoint,
    string Protocol,
    IReadOnlyList<string> Instruments,
    bool IncludeFormattedMessage,
    bool IncludeScopes,
    bool ParseStateValues
);

// Module configuration files: modules never become dependencies of Core, so their defaults are mirrored
// here and StartupSetupTests binds them back to the module records.
public sealed record StartupForumFile(StartupForumSection Forum);

public sealed record StartupForumSection
{
    public string Moderation { get; init; } = "Post";

    public bool AllowBoardOverrides { get; init; }

    public bool AllowUserOverrides { get; init; }
}

public sealed record StartupMarkdownViewerFile(StartupMarkdownViewerSection MarkdownViewer);

public sealed record StartupMarkdownViewerSection(
    string SourceDirectory,
    string OutputDirectory,
    string Title,
    string DefaultDocument
)
{
    public const string DefaultTitle = "Documentation";
    public const string DefaultDocumentName = "README.md";
}
