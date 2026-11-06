using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class NavigationFrontendTests
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
    public void RenderView_ScrollsBackToTheTopBeforeBindingTheNewView()
    {
        var app = Read("app.js");
        var scroll = app.IndexOf("window.scrollTo(0, 0);", StringComparison.Ordinal);

        Assert.True(scroll > app.IndexOf("element.html(view);", StringComparison.Ordinal));
        Assert.True(scroll < app.IndexOf("ko.applyBindings(viewModel, element[0]);", StringComparison.Ordinal));
    }

    [Fact]
    public void Footer_OffersABackToTopButtonThatScrollsByHand()
    {
        Assert.Contains("click: scrollToTop", Read("index.html"));
        Assert.Contains("fa-arrow-up", Read("index.html"));
        Assert.Contains("self.scrollToTop = function", Read("viewmodels/footer.js"));
        Assert.Contains("window.scrollTo({ top: 0, behavior: \"smooth\" })", Read("viewmodels/footer.js"));
    }

    [Theory]
    [InlineData("locales/en-US.json")]
    [InlineData("locales/hu-HU.json")]
    public void Locales_TranslateTheBackToTopLabel(string path)
    {
        Assert.Contains("\"app.backToTop\"", Read(path));
    }
}
