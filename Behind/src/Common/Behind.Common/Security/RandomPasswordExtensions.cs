using System.Security.Cryptography;

namespace Behind.Common.Security;

public static class RandomPasswordExtensions
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789-_.!@#$%^*";

    extension(RandomNumberGenerator)
    {
        public static string CreatePassword(int length = 32) =>
            RandomNumberGenerator.GetString(Alphabet, length);
    }
}
