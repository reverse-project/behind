using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class RouterFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Router_DoesNotRewriteTheAddressWhenAFormIsSubmitted()
    {
        var app = Read("app.js");

        Assert.Contains("var ignoreFormSubmissions = function () {", app);
        Assert.Contains("app._checkFormSubmission = ignoreFormSubmissions;", app);
        Assert.True(
            app.IndexOf("app._checkFormSubmission = ignoreFormSubmissions;", StringComparison.Ordinal)
            < app.IndexOf("patternRoutes.forEach", StringComparison.Ordinal));
    }
}
