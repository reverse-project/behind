using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Behind.Common.Security;

public static class HmacExtensions
{
    extension(string payload)
    {
        public string SignHmacSha256(byte[] key) =>
            Base64Url.EncodeToString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));

        public bool VerifyHmacSha256(string signature, byte[] key) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(payload.SignHmacSha256(key)),
                Encoding.UTF8.GetBytes(signature));
    }

    extension(RandomNumberGenerator)
    {
        public static string CreateUrlSafeToken(int byteLength = 32) =>
            Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(byteLength));
    }
}
