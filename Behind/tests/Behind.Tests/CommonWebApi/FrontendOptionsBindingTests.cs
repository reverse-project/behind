using System.Text;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CommonWebApi;

public sealed class FrontendOptionsBindingTests
{
    [Theory]
    [InlineData("{}", "text/html,text/css,text/javascript,application/javascript,application/manifest+json,image/svg+xml,text/plain")]
    [InlineData("""{"Frontend":{"StaticFiles":{"CompressedMimeTypes":["text/plain"]}}}""", "text/plain")]
    [InlineData("""{"Frontend":{"StaticFiles":{"CompressedMimeTypes":[]}}}""", "")]
    public void ConfiguredMimeTypesReplaceDefaultsInsteadOfAppending(string json, string expected)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        Assert.Equal(expected, string.Join(',', FrontendOptions.Read(configuration).CompressedMimeTypes));
    }
}
