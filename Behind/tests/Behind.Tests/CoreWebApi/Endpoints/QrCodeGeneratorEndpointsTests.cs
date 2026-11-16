using System.Text;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class QrCodeGeneratorEndpointsTests
{
    private const string Uri = "otpauth://totp/Behind:user?secret=JBSWY3DPEHPK3PXP&issuer=Behind";

    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Get_ReturnsTheQrCodeAsSvg()
    {
        var context = new DefaultHttpContext();

        var result = QrCodeGeneratorEndpoints.Get(context, Encode(Uri));

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal(QrCodeGeneratorEndpoints.SvgContentType, content.ContentType);
        Assert.StartsWith("<svg", content.ResponseContent);
    }

    [Fact]
    public void Get_AcceptsTheUrlSafeBase64Alphabet()
    {
        var urlSafe = Encode(Uri + "??>>").TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var result = QrCodeGeneratorEndpoints.Get(new DefaultHttpContext(), urlSafe);

        Assert.IsType<ContentHttpResult>(result);
    }

    [Fact]
    public void Get_DisablesCaching()
    {
        var context = new DefaultHttpContext();

        QrCodeGeneratorEndpoints.Get(context, Encode(Uri));

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("%%%")]
    public void Get_RejectsInvalidBase64(string data)
    {
        Assert.IsType<BadRequest>(QrCodeGeneratorEndpoints.Get(new DefaultHttpContext(), data));
    }

    [Fact]
    public void Get_RejectsContentThatDoesNotFitInAQrCode()
    {
        Assert.IsType<BadRequest>(QrCodeGeneratorEndpoints.Get(new DefaultHttpContext(), Encode(new string('a', 10000))));
    }
}
