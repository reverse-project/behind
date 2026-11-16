using System.Security.Cryptography;
using Hasher = BCrypt.Net.BCrypt;

namespace Behind.Core.Common;

public static class RecoveryCodeExtensions
{
    public const int Count = 20;
    public const int Length = 8;
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const int WorkFactor = 10;

    extension(RandomNumberGenerator)
    {
        public static IReadOnlyList<string> CreateRecoveryCodes(int count = Count) =>
            Enumerable
                .Range(0, count * 2)
                .Select(_ => RandomNumberGenerator.GetString(Alphabet, Length))
                .Distinct()
                .Take(count)
                .ToList();
    }

    extension(string code)
    {
        public string NormalizedRecoveryCode => code.Trim().ToUpperInvariant();

        public bool IsRecoveryCodeShape => code.NormalizedRecoveryCode is { Length: Length } normalized
                                           && normalized.All(Alphabet.Contains);

        public string ToRecoveryCodeHash() => Hasher.EnhancedHashPassword(code.NormalizedRecoveryCode, WorkFactor);

        public bool MatchesRecoveryCodeHash(string hash) =>
            Hasher.EnhancedVerify(code.NormalizedRecoveryCode, hash);
    }
}
