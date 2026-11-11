using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class InvalidationFrontendTests
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
    public void Invalidation_IsEmbeddedAndOffersDisposableAsyncTopicSubscriptions()
    {
        var model = Read("models/invalidation.js");

        Assert.Contains("subscribe: function (topic, handler)", model);
        Assert.Contains("dispose: function ()", model);
        Assert.Contains("invalidate: function (topic)", model);
        Assert.Contains("Promise.allSettled(pending)", model);
        Assert.Contains("throw new AggregateError(errors,", model);
    }

    [Fact]
    public void Menus_InvalidateOnlySuccessfulWritesAndDisposeTheirFollowers()
    {
        var model = Read("models/menus.js");
        var saved = model.IndexOf("var saved = function", StringComparison.Ordinal);
        var failed = model.IndexOf("if (!result.ok)", saved, StringComparison.Ordinal);
        var invalidate = model.IndexOf("invalidation.invalidate(\"menus\")", saved, StringComparison.Ordinal);

        Assert.True(saved >= 0);
        Assert.True(failed > saved);
        Assert.True(invalidate > failed);
        Assert.Contains("invalidation.subscribe(\"menus\"", model);
        Assert.Contains("menu.dispose = function ()", model);
        Assert.Contains("subscription.dispose();", model);
        Assert.Contains("main.dispose();", Read("viewmodels/header.js"));
        Assert.Contains("identitySubscription.dispose();", Read("viewmodels/header.js"));
        Assert.Contains("menu.dispose();", Read("components/menunav.js"));
        Assert.Contains("items.dispose();", Read("components/menunav.js"));
    }
}
