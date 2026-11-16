using Behind.Core.Common.Configuration;
using Behind.Core.Common.Converters;

namespace Behind.Tests.CoreCommon;

public class SecretGenerationTests
{
    [Fact]
    public void Totp_Generate_CreatesAValidSecretOfTheDefaultSize()
    {
        var totp = Totp.Generate();

        Assert.NotEqual(Totp.Invalid, totp);
        Assert.Equal(Totp.DefaultKeySize, OtpNet.Base32Encoding.ToBytes(totp.Value).Length);
    }

    [Fact]
    public void Totp_Generate_CreatesAValueWithoutPadding()
    {
        Assert.DoesNotContain('=', Totp.Generate().Value);
        Assert.DoesNotContain('=', Totp.Generate(32).Value);
    }

    [Fact]
    public void Totp_Generate_UsesTheRequestedSize()
    {
        Assert.Equal(20, OtpNet.Base32Encoding.ToBytes(Totp.Generate(20).Value).Length);
    }

    [Fact]
    public void Totp_Generate_CreatesADifferentSecretEveryTime()
    {
        Assert.NotEqual(Totp.Generate(), Totp.Generate());
    }

    [Fact]
    public void Totp_Generate_CreatesAConfigurationValueThatBindsBack()
    {
        var totp = Totp.Generate();

        var bound = new SecretKeyTypeConverter().ConvertFrom(totp.ToConfigurationValue());

        Assert.Equal(totp, bound);
    }

    [Fact]
    public void JwtSettings_GenerateSigningKey_CreatesALongAlphanumericKey()
    {
        var key = JwtSettings.GenerateSigningKey();

        Assert.Equal(JwtSettings.GeneratedSigningKeyLength, key.Length);
        Assert.All(key, character => Assert.True(char.IsAsciiLetterOrDigit(character)));
    }

    [Fact]
    public void JwtSettings_GenerateSigningKey_CreatesADifferentKeyEveryTime()
    {
        Assert.NotEqual(JwtSettings.GenerateSigningKey(), JwtSettings.GenerateSigningKey());
    }
}
