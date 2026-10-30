using Behind.Common.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Common.WebApi.Security;

public static class DataProtectionExtensions
{
    extension(IServiceCollection services)
    {
        public IDataProtectionBuilder AddCertificateProtectedDataProtection(
            string applicationName,
            string keysPath,
            string certificatePath,
            string certificatePassword) =>
            services
                .AddDataProtection()
                .SetApplicationName(applicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath.EnsureDirectory()))
                .ProtectKeysWithCertificate(certificatePath.LoadOrCreateCertificate(certificatePassword));
    }
}
