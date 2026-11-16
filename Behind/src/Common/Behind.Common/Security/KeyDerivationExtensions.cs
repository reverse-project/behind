using System.Security.Cryptography;
using System.Text;

namespace Behind.Common.Security;

public static class KeyDerivationExtensions
{
    private const int KeyLength = 32;

    extension(Guid secret)
    {
        public byte[] DeriveKey(string pepper, string purpose) =>
            HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                secret.ToByteArray(bigEndian: true),
                KeyLength,
                Encoding.UTF8.GetBytes(pepper),
                Encoding.UTF8.GetBytes(purpose));
    }
}
