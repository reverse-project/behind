using System.Security.Cryptography;
using System.Text;

namespace Behind.Common.Modules;

public static class DeterministicGuidExtensions
{

    extension(Guid)
    {
        public static Guid FromName(string prefix, string name)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
            var tail = Convert.ToHexString(hash, 0, 14).ToLowerInvariant();

            return Guid.Parse($"{prefix}-{tail[..4]}-{tail[4..8]}-{tail[8..12]}-{tail[12..24]}");
        }
    }
}
