using Hasher = BCrypt.Net.BCrypt;

namespace Behind.Core.Common;

public sealed record PasswordHash
{
    private readonly string _hash;

    private PasswordHash(string hash)
    {
        _hash = hash;
    }
    public static implicit operator string(PasswordHash passwordHash) => passwordHash._hash;
    public static implicit operator PasswordHash(string hash) => CreateFromExisting(hash);
    public bool Verify(string plainText) => Hasher.EnhancedVerify(plainText, _hash);
    
    private static string Hash(string plainText) => Hasher.EnhancedHashPassword(plainText);
    
    public static PasswordHash Create(string plainText) => new(Hash(plainText));
    
    public static PasswordHash CreateFromExisting(string hash) => new(hash);
    
    public override string ToString() => _hash;
}