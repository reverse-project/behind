using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Behind.Common.Security;

public static class CertificateFileExtensions
{
    private const string Subject = "CN=Behind DataProtection";
    private const int KeySizeInBits = 3072;
    private const int ValidityInYears = 10;

    extension(string path)
    {
        public X509Certificate2 LoadOrCreateCertificate(string password)
        {
            if (!File.Exists(path))
            {
                try
                {
                    path.CreateCertificateFile(password);
                }
                catch (IOException) when (File.Exists(path))
                {
                }
            }

            return path.LoadCertificate(password);
        }

        public X509Certificate2 LoadCertificate(string password) =>
            X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.DefaultKeySet);

        public string EnsureDirectory()
        {
            Directory.CreateDirectory(path);
            return path;
        }

        public void CreateCertificateFile(string password) =>
            path.WriteNewFile(CreateSelfSignedPfx(password));

        public void WriteNewFile(byte[] content)
        {
            Path.GetDirectoryName(Path.GetFullPath(path))?.EnsureDirectory();
            string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

            try
            {
                using (var stream = new FileStream(temporaryPath, new FileStreamOptions
                       {
                           Mode = FileMode.CreateNew,
                           Access = FileAccess.Write,
                       }))
                {
                    stream.Write(content);
                }

                File.Move(temporaryPath, path, false);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static byte[] CreateSelfSignedPfx(string password)
    {
        using var rsa = RSA.Create(KeySizeInBits);
        var request = new CertificateRequest(Subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        using X509Certificate2 certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(ValidityInYears));

        return certificate.Export(X509ContentType.Pfx, password);
    }
}
