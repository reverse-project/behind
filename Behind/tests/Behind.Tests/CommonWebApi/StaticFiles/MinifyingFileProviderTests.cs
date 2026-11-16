using System.Text;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Tests.Helpers;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CommonWebApi.StaticFiles;

public class MinifyingFileProviderTests
{
    private const string Script = "// comment\nvar answer = 40 + 2;\n";

    private static string ReadAll(IFileInfo file)
    {
        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static MinifyingFileProvider Create(TempFrontendDirectory directory) =>
        new(directory.Provider, ContentMinifiers.Default);

    [Fact]
    public void GetFileInfo_Script_ReturnsMinifiedContentWithMatchingLength()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["app.js"] = Script });

        var file = Create(directory).GetFileInfo("app.js");

        var content = ReadAll(file);
        Assert.True(file.Exists);
        Assert.DoesNotContain("comment", content);
        Assert.Equal(Encoding.UTF8.GetByteCount(content), file.Length);
        Assert.Equal("app.js", file.Name);
    }

    [Fact]
    public void GetFileInfo_AlreadyMinifiedScript_ReturnsOriginal()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["lib.min.js"] = Script });

        Assert.Equal(Script, ReadAll(Create(directory).GetFileInfo("lib.min.js")));
    }

    [Fact]
    public void GetFileInfo_UnsupportedExtension_ReturnsOriginal()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["notes.txt"] = "  spaced   text  " });

        Assert.Equal("  spaced   text  ", ReadAll(Create(directory).GetFileInfo("notes.txt")));
    }

    [Fact]
    public void GetFileInfo_InvalidScript_ReturnsOriginal()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["broken.js"] = "function (" });

        Assert.Equal("function (", ReadAll(Create(directory).GetFileInfo("broken.js")));
    }

    [Fact]
    public void GetFileInfo_MissingFile_DoesNotExist()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string>());

        Assert.False(Create(directory).GetFileInfo("missing.js").Exists);
    }

    [Fact]
    public void GetFileInfo_UnchangedFile_ReturnsCachedInstance()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["app.js"] = Script });
        var provider = Create(directory);

        Assert.Same(provider.GetFileInfo("app.js"), provider.GetFileInfo("app.js"));
    }

    [Fact]
    public void GetFileInfo_ModifiedFile_IsMinifiedAgain()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["app.js"] = Script });
        var provider = Create(directory);
        var first = provider.GetFileInfo("app.js");

        directory.Write("app.js", "// other\nvar changed = 1;\n", DateTime.UtcNow.AddMinutes(5));
        var second = provider.GetFileInfo("app.js");

        Assert.NotSame(first, second);
        Assert.Contains("changed", ReadAll(second));
    }

    [Fact]
    public void GetFileInfo_ConcurrentRequests_AllReceiveMinifiedContent()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["app.js"] = Script });
        var provider = Create(directory);

        var results = Enumerable.Range(0, 32)
            .AsParallel()
            .Select(_ => ReadAll(provider.GetFileInfo("app.js")))
            .ToList();

        Assert.All(results, content => Assert.DoesNotContain("comment", content));
    }

    [Fact]
    public void GetDirectoryContents_DelegatesToInnerProvider()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["views/a.html"] = "<p>a</p>" });

        var contents = Create(directory).GetDirectoryContents("views");

        Assert.Single(contents);
    }
}
