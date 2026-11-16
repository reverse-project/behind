using Behind.Common.WebApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace Behind.Tests.CommonWebApi;

public class AttachmentResponseExtensionsTests
{
    private static IFormFile File(string? contentType)
    {
        var file = new FormFile(new MemoryStream([1]), 0, 1, "files", "a.bin") { Headers = new HeaderDictionary() };

        if (contentType is not null)
        {
            file.ContentType = contentType;
        }

        return file;
    }

    [Theory]
    [InlineData("application/pdf", "application/pdf")]
    [InlineData("text/plain; charset=utf-8", "text/plain")]
    [InlineData("IMAGE/PNG", "IMAGE/PNG")]
    [InlineData("", "application/octet-stream")]
    [InlineData("garbage", "application/octet-stream")]
    [InlineData(null, "application/octet-stream")]
    public void ToStoredContentType_KeepsOnlyTheMediaType(string? contentType, string expected)
    {
        Assert.Equal(expected, File(contentType).ToStoredContentType(100));
    }

    [Fact]
    public void ToStoredContentType_TooLongMediaType_FallsBack()
    {
        var contentType = $"application/{new string('x', 120)}";

        Assert.Equal("application/octet-stream", File(contentType).ToStoredContentType(100));
    }

    [Fact]
    public void ToAttachment_SendsTheFileAsADownloadThatCannotBeSniffedAndMustBeRevalidated()
    {
        var context = new DefaultHttpContext();

        var result = context.ToAttachment("report.html", "text/html", [1, 2]);

        var file = Assert.IsType<FileContentHttpResult>(result);
        Assert.Equal("report.html", file.FileDownloadName);
        Assert.Equal("text/html", file.ContentType);
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal("nosniff", context.Response.Headers[HeaderNames.XContentTypeOptions]);
    }
}
