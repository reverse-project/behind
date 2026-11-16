using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class CarouselFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("models/carousels.js")]
    [InlineData("bindings/carousel.js")]
    [InlineData("viewmodels/carouseleditor.js")]
    [InlineData("components/carouseleditor.js")]
    [InlineData("components/carouseleditor.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void App_RegistersTheCarouselEditorComponent()
    {
        Assert.Contains(
            "ko.components.register(\"carousel-editor\", { require: \"components/carouseleditor\" });",
            Read("app.js"));
    }

    [Fact]
    public void PublicSitePage_ShowsTheCarouselAboveTheContentWhenTheSiteHasOne()
    {
        var view = Read("views/site.html");
        var viewModel = Read("viewmodels/site.js");

        Assert.Contains("data-bind=\"carousel: carousel, visible: carousel\"", view);
        Assert.True(
            view.IndexOf("carousel: carousel", StringComparison.Ordinal)
            < view.IndexOf("html: content", StringComparison.Ordinal));
        Assert.Contains("\"bindings/carousel\"", viewModel);
        Assert.Contains("carousels.loadPublic(result.data.id).then(self.carousel)", viewModel);
    }

    [Fact]
    public void PublicSitePage_MarksALeadingCarouselWhenTheTitleIsHidden()
    {
        Assert.Contains(
            "css: { 'behind-site-leading-carousel': hideTitle() && carousel() }",
            Read("views/site.html"));
    }

    [Fact]
    public void FseTheme_RemovesTheTopMarginAboveALeadingCarousel()
    {
        var css = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Sites", "Behind.Sites.FSE", "wwwroot", "css", "fse.css"));

        Assert.Contains("#main-container:has(.behind-site-leading-carousel)", css);
        Assert.Contains("margin-top: 0 !important;", css);
    }

    [Fact]
    public void SiteEditor_OffersTheCarouselEditorOnlyForExistingSites()
    {
        var view = Read("views/siteeditor.html");

        Assert.Contains("<div data-bind=\"if: !isNew\">", view);
        Assert.Contains("<carousel-editor params=\"siteId: siteId\"></carousel-editor>", view);
        Assert.Contains("self.siteId = siteId;", Read("viewmodels/siteeditor.js"));
    }

    [Fact]
    public void Binding_UsesTheBootstrapCarouselWithAnAutomaticInterval()
    {
        var binding = Read("bindings/carousel.js");

        Assert.Contains("define([\"ko\", \"bootstrap\", \"models/i18n\"]", binding);
        Assert.Contains("ko.bindingHandlers.carousel", binding);
        Assert.Contains("new bootstrap.Carousel(root", binding);
        Assert.Contains("interval: data.intervalSeconds * 1000", binding);
        Assert.Contains("ride: slides > 1 && !prefersReducedMotion() ? \"carousel\" : false", binding);
        Assert.Contains("pause: \"hover\"", binding);
        Assert.Contains("instance.dispose()", binding);
    }

    [Fact]
    public void Binding_WritesCaptionsAsTextAndNeverAsHtml()
    {
        var binding = Read("bindings/carousel.js");

        Assert.Contains("text.textContent = slide.caption;", binding);
        Assert.DoesNotContain("innerHTML", binding);
        Assert.DoesNotContain(".html(", binding);
    }

    [Fact]
    public void Model_UsesTheCarouselEndpointsWithTheirOwnImageUpload()
    {
        var model = Read("models/carousels.js");

        Assert.Contains("\"/carousels/settings\"", model);
        Assert.Contains("\"/carousels/sites/\" + siteId", model);
        Assert.Contains("\"/carousels/images\"", model);
        Assert.Contains("\"/sites/\" + siteId + \"/carousel\"", model);
        Assert.Contains("\"/delete\"", model);
        Assert.Contains("\"/restore\"", model);
        Assert.Contains("\"/hard/delete\"", model);
        Assert.Contains("?deleted=true", model);
    }

    [Fact]
    public void Editor_OffersSoftDeleteRestoreAndPermanentDelete()
    {
        var view = Read("components/carouseleditor.html");
        var viewModel = Read("viewmodels/carouseleditor.js");

        Assert.Contains("click: remove", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("session.hasGrant(\"Site.Delete\")", viewModel);
        Assert.Contains("session.hasGrant(\"RawDatabase.Manage\")", viewModel);
    }

    [Fact]
    public void Editor_TakesEveryLimitFromTheConfiguredSettings()
    {
        var view = Read("components/carouseleditor.html");
        var viewModel = Read("viewmodels/carouseleditor.js");

        Assert.Contains("attr: { min: minInterval, max: maxInterval }", view);
        Assert.Contains("attr: { maxlength: $parent.captionLimit }", view);
        Assert.Contains("attr: { maxlength: $parent.linkLimit }", view);
        Assert.Contains("self.settings().maxSlides", viewModel);
        Assert.Contains("self.settings().defaultIntervalSeconds", viewModel);
        Assert.Contains("self.settings().minIntervalSeconds", viewModel);
        Assert.Contains("self.settings().maxIntervalSeconds", viewModel);
        Assert.Contains("self.settings().maxCaptionLength", viewModel);
        Assert.DoesNotContain("min=\"2\"", view);
        Assert.DoesNotContain("max=\"60\"", view);
    }

    [Fact]
    public void Editor_IsNotGatedByAGlobalSwitch()
    {
        Assert.DoesNotContain("enabled", Read("viewmodels/carouseleditor.js"));
        Assert.DoesNotContain("enabled", Read("components/carouseleditor.html"));
        Assert.DoesNotContain("enabled", Read("models/carousels.js"));
    }

    [Fact]
    public void Locales_TranslateEveryCarouselKeyInBothLanguages()
    {
        var sources = string.Concat(
            Read("bindings/carousel.js"),
            Read("components/carouseleditor.html"),
            Read("viewmodels/carouseleditor.js"),
            Read("models/carousels.js"));
        var keys = System.Text.RegularExpressions.Regex
            .Matches(sources, "[\"'](carousels?\\.[A-Za-z0-9.]+)[\"']")
            .Select(match => match.Groups[1].Value)
            .Concat(["audit.entities.Carousel", "audit.entities.CarouselSlide"])
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);

        foreach (var locale in new[] { "en-US", "hu-HU" })
        {
            using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
            var messages = document.RootElement.GetProperty("messages");

            foreach (var key in keys)
            {
                Assert.True(messages.TryGetProperty(key, out var value) && value.GetString()?.Length > 0, $"{locale}: {key}");
            }
        }
    }
}
