using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class HttpETagFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string ReadHttp()
    {
        var file = Provider.GetFileInfo("models/http.js");
        Assert.True(file.Exists);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Http_SendsIfNoneMatchForCachedGetResponses()
    {
        var http = ReadHttp();

        Assert.Contains("\"If-None-Match\": cached.etag", http);
        Assert.Contains("xhr.getResponseHeader(\"ETag\")", http);
    }

    [Fact]
    public void Http_ReusesTheCachedBodyOnNotModified()
    {
        var http = ReadHttp();

        Assert.Contains("cached && xhr.status === 304", http);
        Assert.Contains("parse(cached.text)", http);
    }

    [Fact]
    public void Http_OnlyCachesPlainGetRequestsOtherThanTheSession()
    {
        var http = ReadHttp();

        Assert.Contains("method === \"GET\" && url !== sessionUrl && $.isEmptyObject(extraHeaders || {})", http);
    }

    [Fact]
    public void Http_KeepsTheCacheApartPerAccessTokenAndBounded()
    {
        var http = ReadHttp();

        Assert.Contains("responseCache.owner !== token", http);
        Assert.Contains("maxCachedResponses", http);
        Assert.Contains("entries.delete(url)", http);
    }
}
