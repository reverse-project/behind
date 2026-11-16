using System.Security.Cryptography;
using Behind.Common.Security;

namespace Behind.Tests.Common.Security;

public class HmacExtensionsTests
{
    private static readonly byte[] Key = Guid.Parse("019e5f3b-dc22-7d66-8cd3-9cb754d0fd28").DeriveKey("pepper", "test");

    [Fact]
    public void SignHmacSha256_IsDeterministicAndUrlSafe()
    {
        var first = "payload".SignHmacSha256(Key);
        var second = "payload".SignHmacSha256(Key);

        Assert.Equal(first, second);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }

    [Fact]
    public void SignHmacSha256_DiffersByPayloadAndKey()
    {
        var signature = "payload".SignHmacSha256(Key);

        Assert.NotEqual(signature, "other".SignHmacSha256(Key));
        Assert.NotEqual(signature, "payload".SignHmacSha256(Guid.NewGuid().DeriveKey("pepper", "test")));
    }

    [Fact]
    public void VerifyHmacSha256_ValidSignature_ReturnsTrue() =>
        Assert.True("payload".VerifyHmacSha256("payload".SignHmacSha256(Key), Key));

    [Fact]
    public void VerifyHmacSha256_TamperedPayloadOrKeyOrSignature_ReturnsFalse()
    {
        var signature = "payload".SignHmacSha256(Key);

        Assert.False("tampered".VerifyHmacSha256(signature, Key));
        Assert.False("payload".VerifyHmacSha256(signature, Guid.NewGuid().DeriveKey("pepper", "test")));
        Assert.False("payload".VerifyHmacSha256("garbage", Key));
        Assert.False("payload".VerifyHmacSha256(string.Empty, Key));
    }

    [Fact]
    public void CreateUrlSafeToken_ReturnsDistinctUrlSafeValues()
    {
        var first = RandomNumberGenerator.CreateUrlSafeToken();
        var second = RandomNumberGenerator.CreateUrlSafeToken();

        Assert.NotEqual(first, second);
        Assert.Matches("^[A-Za-z0-9_-]+$", first);
    }

    [Fact]
    public void CreateUrlSafeToken_HonoursByteLength() =>
        Assert.Equal(Convert.ToBase64String(new byte[16]).TrimEnd('=').Length,
            RandomNumberGenerator.CreateUrlSafeToken(16).Length);
}
