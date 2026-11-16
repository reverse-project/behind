using System.Security.Cryptography;
using Behind.Core.Common;

namespace Behind.Tests.CoreCommon;

public class RecoveryCodeExtensionsTests
{
    [Fact]
    public void CreateRecoveryCodes_ByDefaultCreatesTwentyCodesOfEightLettersAndNumbers()
    {
        var codes = RandomNumberGenerator.CreateRecoveryCodes();

        Assert.Equal(20, codes.Count);
        Assert.All(codes, code =>
        {
            Assert.Equal(8, code.Length);
            Assert.All(code, character => Assert.True(char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character)));
        });
    }

    [Fact]
    public void CreateRecoveryCodes_CreatesDistinctCodes()
    {
        var codes = RandomNumberGenerator.CreateRecoveryCodes();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void CreateRecoveryCodes_CreatesADifferentBunchEveryTime()
    {
        Assert.NotEqual(RandomNumberGenerator.CreateRecoveryCodes(), RandomNumberGenerator.CreateRecoveryCodes());
    }

    [Fact]
    public void CreateRecoveryCodes_HonoursTheRequestedCount()
    {
        Assert.Equal(3, RandomNumberGenerator.CreateRecoveryCodes(3).Count);
    }

    [Theory]
    [InlineData("ABCD2345", true)]
    [InlineData("  abcd2345 ", true)]
    [InlineData("ABCD234", false)]
    [InlineData("ABCD23456", false)]
    [InlineData("ABCD-345", false)]
    [InlineData("", false)]
    public void IsRecoveryCodeShape_AcceptsEightLettersAndNumbersIgnoringCaseAndPadding(string code, bool expected)
    {
        Assert.Equal(expected, code.IsRecoveryCodeShape);
    }

    [Fact]
    public void MatchesRecoveryCodeHash_AcceptsTheCodeIgnoringCaseAndPadding()
    {
        var hash = "ABCD2345".ToRecoveryCodeHash();

        Assert.True(" abcd2345 ".MatchesRecoveryCodeHash(hash));
    }

    [Fact]
    public void MatchesRecoveryCodeHash_RejectsAnotherCode()
    {
        Assert.False("ABCD2346".MatchesRecoveryCodeHash("ABCD2345".ToRecoveryCodeHash()));
    }

    [Fact]
    public void ToRecoveryCodeHash_IsSaltedAndDoesNotContainTheCode()
    {
        var first = "ABCD2345".ToRecoveryCodeHash();
        var second = "ABCD2345".ToRecoveryCodeHash();

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("ABCD2345", first);
    }
}
