using Behind.Common;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Tools;

public sealed record DataProtectionSetupRequest(
    string CertificatePath,
    string KeysPath,
    string Password,
    Option<string> OutputPath,
    bool Force
);

public sealed record SecurityFile(SecuritySection Security);

public sealed record SecuritySection(DataProtectionSettings DataProtection);
