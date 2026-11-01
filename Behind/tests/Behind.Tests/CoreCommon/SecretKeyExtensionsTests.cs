using Behind.Core.Common.Configuration;
using OtpNet;

namespace Behind.Tests.CoreCommon;

public class SecretKeyExtensionsTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 20).Select(i => (byte)i).ToArray();
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private static Option<SecretKey> Secret()
    {
        Behind.Core.Common.Configuration.Totp.TryCreate(Base32Encoding.ToString(Key), out var totp);
        return Some<SecretKey>(totp);
    }

    private static string CodeFor(OtpHashMode mode, DateTimeOffset at) =>
        new OtpNet.Totp(Key, mode: mode).ComputeTotp(at.UtcDateTime);

    [Fact]
    public void HashMode_IsSha256()
    {
        Assert.Equal(OtpHashMode.Sha256, Behind.Core.Common.Configuration.Totp.HashMode);
    }

    [Fact]
    public void VerifyTotp_Sha256Code_IsAccepted()
    {
        Assert.True(Secret().VerifyTotp(CodeFor(OtpHashMode.Sha256, Now), Now));
    }

    [Theory]
    [InlineData(OtpHashMode.Sha1)]
    [InlineData(OtpHashMode.Sha512)]
    public void VerifyTotp_CodeFromAnotherHashMode_IsRejected(OtpHashMode mode)
    {
        Assert.False(Secret().VerifyTotp(CodeFor(mode, Now), Now));
    }

    [Fact]
    public void VerifyTotp_CodeFromAnotherSecret_IsRejected()
    {
        var other = new OtpNet.Totp(Enumerable.Range(50, 20).Select(i => (byte)i).ToArray(), mode: OtpHashMode.Sha256)
            .ComputeTotp(Now.UtcDateTime);

        Assert.False(Secret().VerifyTotp(other, Now));
    }

    [Fact]
    public void VerifyTotp_ExpiredCode_IsRejected()
    {
        Assert.False(Secret().VerifyTotp(CodeFor(OtpHashMode.Sha256, Now.AddMinutes(-10)), Now));
    }

    [Fact]
    public void VerifyTotp_NoSecret_IsRejected()
    {
        Option<SecretKey> none = None;

        Assert.False(none.VerifyTotp(CodeFor(OtpHashMode.Sha1, Now), Now));
    }

    [Fact]
    public void VerifyTotp_NonTotpSecret_IsRejected()
    {
        Option<SecretKey> uuid = Some<SecretKey>(Uuid.CreateVersion7(Now));

        Assert.False(uuid.VerifyTotp(CodeFor(OtpHashMode.Sha1, Now), Now));
    }
}
