using Behind.Core.Common.Configuration;
using OtpNet;

namespace Behind.Tests.CoreCommon;

public class TotpVerifiedStepTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 20).Select(i => (byte)i).ToArray();
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private static Behind.Core.Common.Configuration.Totp Secret()
    {
        Behind.Core.Common.Configuration.Totp.TryCreate(Base32Encoding.ToString(Key), out var totp);
        return totp;
    }

    [Fact]
    public void UserHashMode_IsSha1BecauseAuthenticatorAppsIgnoreTheAlgorithm()
    {
        Assert.Equal(OtpHashMode.Sha1, Behind.Core.Common.Configuration.Totp.UserHashMode);
    }

    [Fact]
    public void VerifiedStep_ReturnsTheMatchedTimeStep()
    {
        var code = new OtpNet.Totp(Key).ComputeTotp(Now.UtcDateTime);

        var step = Secret().VerifiedStep(code, Now, OtpHashMode.Sha1);

        Assert.Equal(Some(Now.ToUnixTimeSeconds() / 30), step);
    }

    [Fact]
    public void VerifiedStep_ReturnsAnEarlierStepForACodeWithinTheWindow()
    {
        var code = new OtpNet.Totp(Key).ComputeTotp(Now.UtcDateTime.AddSeconds(-30));

        var step = Secret().VerifiedStep(code, Now, OtpHashMode.Sha1, 1);

        Assert.Equal(Some(Now.ToUnixTimeSeconds() / 30 - 1), step);
    }

    [Fact]
    public void VerifiedStep_WithoutToleranceRejectsAnEarlierStep()
    {
        var code = new OtpNet.Totp(Key).ComputeTotp(Now.UtcDateTime.AddSeconds(-30));

        Assert.Equal(None, Secret().VerifiedStep(code, Now, OtpHashMode.Sha1));
    }

    [Fact]
    public void VerifiedStep_RejectsAWrongCode()
    {
        Assert.Equal(None, Secret().VerifiedStep("000000", Now, OtpHashMode.Sha1));
    }

    [Fact]
    public void VerifiedStep_RejectsACodeOfAnotherHashMode()
    {
        var code = new OtpNet.Totp(Key, mode: OtpHashMode.Sha256).ComputeTotp(Now.UtcDateTime);

        Assert.Equal(None, Secret().VerifiedStep(code, Now, OtpHashMode.Sha1));
    }

    [Fact]
    public void VerifiedStep_DefaultsToTheElevationHashMode()
    {
        var code = new OtpNet.Totp(Key, mode: OtpHashMode.Sha256).ComputeTotp(Now.UtcDateTime);

        Assert.NotEqual(None, Secret().VerifiedStep(code, Now));
    }

    [Fact]
    public void ToOtpUri_CanDeclareSha1()
    {
        var uri = new Uri(Secret().ToOtpUri("ada@test.com", "Behind System", OtpHashMode.Sha1));

        Assert.Equal("sha1", System.Web.HttpUtility.ParseQueryString(uri.Query)["algorithm"]);
    }
}
