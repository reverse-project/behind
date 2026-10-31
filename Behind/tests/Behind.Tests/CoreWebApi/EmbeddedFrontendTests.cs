using System.Text;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class EmbeddedFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static IEnumerable<IFileInfo> Walk(string path) =>
        Provider.GetDirectoryContents(path).SelectMany(entry =>
            entry.IsDirectory ? Walk($"{path.TrimEnd('/')}/{entry.Name}") : [entry]);

    private static string Read(IFileInfo file)
    {
        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("index.html")]
    [InlineData("bootloader.js")]
    [InlineData("config.js")]
    [InlineData("app.js")]
    [InlineData("libraries/require.min.js")]
    [InlineData("libraries/jquery.min.js")]
    [InlineData("libraries/knockout.min.js")]
    [InlineData("libraries/sammy.min.js")]
    [InlineData("libraries/text.js")]
    [InlineData("libraries/domReady.js")]
    [InlineData("views/index.html")]
    [InlineData("controllers/index.js")]
    [InlineData("viewmodels/index.js")]
    [InlineData("models/session.js")]
    [InlineData("controllers/magiclink.js")]
    [InlineData("viewmodels/magiclink.js")]
    [InlineData("views/magiclink.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void IndexHtml_LoadsRequireWithBootloader()
    {
        var index = Read(Provider.GetFileInfo("index.html"));

        Assert.Contains("libraries/require.min.js", index);
        Assert.Contains("data-main=\"bootloader.js\"", index);
    }

    [Fact]
    public void ConfigPaths_PointToExistingLibraries()
    {
        var config = Read(Provider.GetFileInfo("config.js"));

        var paths = System.Text.RegularExpressions.Regex
            .Matches(config, "\"?[\\w-]+\"?: \"(libraries/[^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(paths);
        Assert.All(paths, path => Assert.True(Provider.GetFileInfo($"{path}.js").Exists, path));
    }

    [Fact]
    public void ApplicationSources_AreMinifiable()
    {
        var sources = Walk("/")
            .Where(file => !file.Name.Contains(".min."))
            .ToList();

        var failures = ContentMinifiers.Default
            .SelectMany(minifier => sources
                .Where(file => minifier.Extensions.Contains(Path.GetExtension(file.Name)))
                .Where(file => minifier.Minify(Read(file)).Equals(None))
                .Select(file => file.Name))
            .ToList();

        Assert.NotEmpty(sources);
        Assert.Empty(failures);
    }

    [Fact]
    public void Views_AreMinifiedThroughProvider()
    {
        var provider = new MinifyingFileProvider(Provider, ContentMinifiers.Default);

        var original = Read(Provider.GetFileInfo("views/signin.html"));
        var minified = Read(provider.GetFileInfo("views/signin.html"));

        Assert.True(minified.Length < original.Length);
        Assert.Contains("data-bind", minified);
    }
}
