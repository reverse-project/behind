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
    bool SkipExisting
)
{
    public const string SecurityFileName = "security.json";
    public const string DatabaseFileName = "database.json";
    public const string BackendFileName = "backend.json";
    public const string CertificateFileName = "dataprotection.pfx";
    public const string KeysDirectoryName = "DataProtection-Keys";
    public const string DatabaseFileNameInData = "web.db";
    public const string ConnectionStringName = "DefaultConnection";
    public const string DefaultIssuer = "Behind Backend";
    public const string DefaultAudience = "Behind Frontend";
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
    bool EmailConfirmation,
    DataProtectionSettings DataProtection,
    SecretKey SecretKey,
    JwtSettings Jwt,
    ElevationSettings Elevation,
    StartupMagicLinkSettings MagicLink
);

public sealed record StartupMagicLinkSettings(SecretKey ActualSecret);

public sealed record StartupDatabaseFile(
    IReadOnlyDictionary<string, string> ConnectionStrings,
    DatabaseConfiguration Database
);

public sealed record StartupBackendFile(StartupBackendSection Backend);

public sealed record StartupBackendSection(string DataDirectory);
