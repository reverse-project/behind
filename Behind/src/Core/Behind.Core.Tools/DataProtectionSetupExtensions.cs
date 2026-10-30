using System.Text.Json;
using Behind.Common;
using Behind.Common.Security;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Tools;

public static class DataProtectionSetupExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    extension(DataProtectionSettings settings)
    {
        public string ToSecurityJson() =>
            JsonSerializer.Serialize(new SecurityFile(new SecuritySection(settings)), JsonOptions);
    }

    extension(DataProtectionSetupRequest request)
    {
        public DataProtectionSettings CreateCertificate()
        {
            if (request.Force)
            {
                File.Delete(request.CertificatePath);
            }

            request.CertificatePath.CreateCertificateFile(request.Password);

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
                    if (request.Force)
                    {
                        File.Delete(path);
                    }

                    path.WriteNewFile(System.Text.Encoding.UTF8.GetBytes(json));
                    output.WriteLine("Certificate: {0}", Path.GetFullPath(request.CertificatePath));
                    output.WriteLine("Security configuration: {0}", Path.GetFullPath(path));
                });
        }
    }
}
