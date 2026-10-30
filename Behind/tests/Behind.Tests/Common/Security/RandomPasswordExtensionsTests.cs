using System.Security.Cryptography;
using Behind.Common.Security;

namespace Behind.Tests.Common.Security;

public sealed class RandomPasswordExtensionsTests
{
    [Fact]
    public void CreatePassword_DefaultLength_Is32()
        => Assert.Equal(32, RandomNumberGenerator.CreatePassword().Length);

    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    public void CreatePassword_CustomLength_IsRespected(int length)
        => Assert.Equal(length, RandomNumberGenerator.CreatePassword(length).Length);

    [Fact]
    public void CreatePassword_TwoCalls_AreDifferent()
        => Assert.NotEqual(RandomNumberGenerator.CreatePassword(), RandomNumberGenerator.CreatePassword());
}
