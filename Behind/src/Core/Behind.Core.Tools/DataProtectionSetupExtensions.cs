using Behind.Common;
using Behind.Common.Security;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Tools;

public static class DataProtectionSetupExtensions
{
    extension(DataProtectionSettings settings)
    {
        public string ToSecurityJson() =>
            new SecurityFile(new SecuritySection(settings)).ToConfigurationJson();
    }

    extension(DataProtectionSetupRequest request)
    {
        public DataProtectionSettings CreateCertificate()
        {
            request.CertificatePath.CreateCertificateFileDirect(request.Password, request.Force);

            return new DataProtectionSettings(
                Path.GetFullPath(request.KeysPath),
                Path.GetFullPath(request.CertificatePath),
                request.Password
            );
        }

        public void Run(TextWriter output)
        {
            string json = request.CreateCertificate().ToSecurityJson();

            request.OutputPath.Match(
                none: () => output.WriteLine(json),
                path =>
                {
                    path.WriteFileDirect(System.Text.Encoding.UTF8.GetBytes(json), request.Force);
                    output.WriteLine("Certificate: {0}", Path.GetFullPath(request.CertificatePath));
                    output.WriteLine("Security configuration: {0}", Path.GetFullPath(path));
                });
        }
    }
}
