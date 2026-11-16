using static Behind.Common.FunctionalExtensions;

namespace Behind.Tests.Common;

public class QrCodeExtensionsTests
{
    [Fact]
    public void ToQrCodeSvg_CreatesAnSvgDocument()
    {
        var svg = "otpauth://totp/Behind:user?secret=JBSWY3DPEHPK3PXP".ToQrCodeSvg().GetOrElse(string.Empty);

        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg.TrimEnd());
    }

    [Fact]
    public void ToQrCodeSvg_IsDeterministic()
    {
        Assert.Equal("secret".ToQrCodeSvg(), "secret".ToQrCodeSvg());
    }

    [Fact]
    public void ToQrCodeSvg_ReturnsNoneWhenTheContentDoesNotFit()
    {
        Assert.Equal(None, new string('a', 10000).ToQrCodeSvg());
    }
}
