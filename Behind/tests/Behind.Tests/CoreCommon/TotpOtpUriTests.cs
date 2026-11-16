using Behind.Core.Common.Configuration;
using OtpNet;

namespace Behind.Tests.CoreCommon;

public class TotpOtpUriTests
{
    private static Behind.Core.Common.Configuration.Totp Secret()
    {
        Behind.Core.Common.Configuration.Totp.TryCreate(
            Base32Encoding.ToString(Enumerable.Range(1, 20).Select(i => (byte)i).ToArray()),
            out var totp);
        return totp;
    }

    [Fact]
    public void ToOtpUri_DeclaresSha256SixDigitsThirtySeconds()
    {
        var uri = new Uri(Secret().ToOtpUri("admin", "Behind System"));
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal("otpauth", uri.Scheme);
        Assert.Equal("sha256", query["algorithm"]);
        Assert.Equal("6", query["digits"]);
        Assert.Equal("30", query["period"]);
        Assert.Equal("Behind System", query["issuer"]);
        Assert.Equal("/Behind System:admin", Uri.UnescapeDataString(uri.AbsolutePath));
    }

    [Fact]
    public void ToOtpUri_SecretIsUnpaddedBase32OfTheKey()
    {
        var totp = Secret();
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(totp.ToOtpUri("a", "b")).Query);

        Assert.DoesNotContain("=", query["secret"]);
        Assert.Equal(totp.Value.TrimEnd('='), query["secret"]);
    }

    [Fact]
    public void ToConfigurationValue_RoundTripsThroughTheSecretKeyConverter()
    {
        var totp = Secret();

        var parsed = new Behind.Core.Common.Converters.SecretKeyTypeConverter().ConvertFrom(totp.ToConfigurationValue());

        Assert.Equal(totp, parsed);
    }
}
