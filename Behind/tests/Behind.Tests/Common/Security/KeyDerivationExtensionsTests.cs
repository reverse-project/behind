using Behind.Common.Security;

namespace Behind.Tests.Common.Security;

public class KeyDerivationExtensionsTests
{
    private static readonly Guid Secret = Guid.Parse("019e5f3b-dc22-7d66-8cd3-9cb754d0fd28");

    [Fact]
    public void DeriveKey_IsDeterministicAnd256Bit()
    {
        var first = Secret.DeriveKey("pepper", "purpose");

        Assert.Equal(first, Secret.DeriveKey("pepper", "purpose"));
        Assert.Equal(32, first.Length);
    }

    [Fact]
    public void DeriveKey_DiffersByPepperPurposeAndSecret()
    {
        var key = Secret.DeriveKey("pepper", "purpose");

        Assert.NotEqual(key, Secret.DeriveKey("other", "purpose"));
        Assert.NotEqual(key, Secret.DeriveKey("pepper", "other"));
        Assert.NotEqual(key, Guid.NewGuid().DeriveKey("pepper", "purpose"));
    }
}
