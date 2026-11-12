using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Behind.Core.WebApi;

namespace Behind.Tests.CoreWebApi;

public sealed class LayoutsFrontendTests
{
    private static string Read(string path)
    {
        var file = EmbeddedFrontend.CreateFileProvider().GetFileInfo(path);
        Assert.True(file.Exists, path);
        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("controllers/layouts.js")]
    [InlineData("viewmodels/layouts.js")]
    [InlineData("views/layouts.html")]
    [InlineData("models/layouts.js")]
    [InlineData("models/layoutshell.js")]
    public void EmbeddedFrontend_ContainsLayoutSurface(string path) => Assert.NotEmpty(Read(path));

    [Fact]
    public void CustomWidgets_EnablePicturesAndReuseRichContentStyles()
    {
        Assert.Contains("upload: $root.uploadImage", Read("views/layouts.html"));
        Assert.Contains("class=\"site-content\" data-bind=\"html: content\"", Read("components/sitewidget.html"));
        Assert.Contains(".behind-widget .site-content", Read("css/app.css"));
    }

    [Fact]
    public void VerticalMenuWidgets_RenderChildrenWithTheirOwnHierarchicalVariant()
    {
        Assert.Contains("? 'vertical' : 'nav'", Read("components/sitewidget.html"));
        Assert.Contains("isVertical: params.variant === \"vertical\"", Read("components/menuitems.js"));
        Assert.Contains("hasChildren && $component.isVertical", Read("components/menuitems.html"));
        Assert.Contains("items: children, variant: 'vertical'", Read("components/menuitems.html"));
        Assert.Contains(".menu-vertical .menu-vertical", Read("css/app.css"));
    }

    [Fact]
    public void App_WiresEditorShellAndWidgetComponent()
    {
        var app = Read("app.js");
        Assert.Contains("\"#/manage/layouts\": \"controllers/layouts\"", app);
        Assert.Contains("ko.components.register(\"site-widget\"", app);
        Assert.Contains("layoutShell.start(context)", app);
        Assert.Contains("layoutShell.route(", app);
        Assert.Contains("#/manage/layouts", Read("viewmodels/administration.js"));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_TranslateAllLayoutTexts(string locale)
    {
        var sources = Read("views/layouts.html") + Read("viewmodels/layouts.js") + Read("models/layoutshell.js");
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
        var messages = document.RootElement.GetProperty("messages");
        var keys = Regex.Matches(sources, "[\"'](layouts\\.[A-Za-z0-9.]+)[\"']")
            .Select(match => match.Groups[1].Value).Distinct();
        foreach (var key in keys)
        {
            Assert.True(messages.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()), key);
        }
    }

    [Fact]
    public void Grid_UsesResponsiveStackingAndUnconstrainedContent()
    {
        var css = Read("css/app.css");
        Assert.Contains(".behind-layout-section", css);
        Assert.Contains(".behind-layout .behind-layout-content", css);
        Assert.Contains(".behind-layout .behind-layout-section", css);
        Assert.Contains("flex-direction: column", css);
        Assert.Contains(".behind-layout .behind-layout-section-overlapping", css);
        Assert.Contains("display: grid", css);
        Assert.Contains("isolation: isolate", css);
    }

    [Fact]
    public void CellAppearanceOptions_ExposeIndependentCheckboxesAndScopedFrameStyles()
    {
        var view = Read("views/layouts.html");
        var css = Read("css/app.css");
        foreach (var option in new[] { "borderless", "noMargin", "noPadding" })
        {
            Assert.Contains($"checked: {option}", view);
            Assert.Contains($"layouts.{option}", view);
        }
        Assert.Contains(".behind-layout-cell-borderless > .behind-widget > .card-header", css);
        Assert.Contains(".behind-layout-cell-no-margin > :is(.behind-widget, .behind-layout-content", css);
        Assert.Contains(".behind-layout-cell-no-padding > .behind-widget > :is(.card-header, .card-body)", css);
        Assert.Contains(".behind-layout-cell-no-padding > :is(.behind-page-header, .behind-page-footer) > *", css);
        Assert.DoesNotContain(".behind-layout-cell-no-padding *", css);
        Assert.Contains("border: 0 !important", css);
        Assert.Contains("margin: 0 !important", css);
        Assert.Contains("padding: 0 !important", css);
    }

    [Fact]
    public void HeaderWidgets_ExposeDefaultLeftAndRightHamburgerPositions()
    {
        var view = Read("views/layouts.html");
        Assert.Contains("value: hamburgerPosition", view);
        foreach (var position in new[] { "default", "left", "right" })
        {
            Assert.Contains($"value=\"{position}\"", view);
        }
        var css = Read("css/app.css");
        Assert.Contains("[data-hamburger-position=\"left\"] .navbar-toggler", css);
        Assert.Contains("[data-hamburger-position=\"right\"] .navbar-toggler", css);
        Assert.DoesNotContain("[data-hamburger-position=\"default\"]", css);
    }

    [Fact]
    public void HeaderLocaleSwitch_UsesItsPersistedPresentation()
    {
        var editor = Read("views/layouts.html");
        Assert.Contains("visible: type() === 'core.header' || type() === 'core.locale-switch'", editor);
        Assert.Contains("value: localeSwitchView", editor);
        var shell = Read("models/layoutshell.js");
        Assert.Contains("localeSwitchView: widget.localeSwitchView", shell);
        Assert.Contains("mountHeader(header, configuredHeader())", shell);
        Assert.Contains("return found || section.widgets.find", shell);
        var header = Read("components/siteheader.js");
        Assert.Contains("localeSwitchView = ko.unwrap((params || {}).localeSwitchView) || \"view\"", header);
        Assert.Contains("<locale-switch params=\"view: localeSwitchView\"></locale-switch>", Read("components/siteheader.html"));
        Assert.Contains("localeSwitchView: widget.localeSwitchView()", Read("viewmodels/layouts.js"));
    }

    [Fact]
    public void Badges_UseAnAbsoluteClickThroughOverlayAboveLayoutSections()
    {
        var css = Read("css/app.css");
        var overlay = css.Split(".behind-layout-badges {", StringSplitOptions.None)[1].Split('}')[0];
        Assert.Contains("position: absolute", overlay);
        Assert.Contains("z-index: 1100", overlay);
        Assert.Contains("pointer-events: none", overlay);
        var badge = css.Split(".behind-layout-badge {", StringSplitOptions.None)[1].Split('}')[0];
        Assert.Contains("pointer-events: auto", badge);
        Assert.Contains("max-width: 100%", badge);
        Assert.Contains("max-width: 50%", css);
        Assert.DoesNotContain("position: fixed", overlay);
        var shell = Read("models/layoutshell.js");
        Assert.Contains("element.textContent = badge.text", shell);
        Assert.Contains("image.setAttribute(\"alt\", badge.text)", shell);
        Assert.Contains("renderBadges(layout.badges)", shell);
        var view = Read("views/layouts.html");
        Assert.Contains("foreach: badges", view);
        Assert.Contains("change: $root.uploadBadge", view);
        Assert.Contains("click: $root.removeBadge", view);
    }

    [Fact]
    public void ImageBadges_HaveNoOuterSpacingWhileTextBadgesKeepTheirInset()
    {
        var css = Read("css/app.css");
        foreach (var selector in new[] { ".behind-layout-badge", ".behind-layout-badge img" })
        {
            var rule = css.Split(selector + " {", StringSplitOptions.None)[1].Split('}')[0];
            Assert.Contains("margin: 0;", rule);
            Assert.Contains("padding: 0;", rule);
        }
        foreach (var corner in new[] { "top-left", "top-right", "bottom-left", "bottom-right" })
        {
            var rule = css.Split(".behind-layout-badge-" + corner + " {", StringSplitOptions.None)[1].Split('}')[0];
            foreach (var edge in corner.Split('-'))
            {
                Assert.Contains(edge + ": 0;", rule);
            }
        }
        var stack = css.Split(".behind-layout-badge-corner {", StringSplitOptions.None)[1].Split('}')[0];
        Assert.DoesNotContain("gap:", stack);
        var text = css.Split(".behind-layout-badge-text {", StringSplitOptions.None)[1].Split('}')[0];
        Assert.Contains("margin: 0.5rem;", text);
        Assert.Contains("max-width: calc(100% - 1rem);", text);
    }

    [Fact]
    public void Shell_WrapsSectionsByWidth()
    {
        var shell = Read("models/layoutshell.js");
        Assert.Contains("section.width === \"full\"", shell);
        Assert.Contains("\"container behind-layout-band\"", shell);
        Assert.Contains("behind-layout-section-full", shell);
        Assert.Contains("behind-layout-section-site", shell);
        Assert.DoesNotContain("container my-4", shell);
        var css = Read("css/app.css");
        Assert.Contains(".behind-layout-section-full", css);
        Assert.Contains(".behind-layout-band", css);
        var band = css.Split(".behind-layout-band {", StringSplitOptions.None)[1].Split('}')[0];
        Assert.DoesNotContain("max-width", band);
        Assert.Contains("hasWidget(section, \"core.footer\")", shell);
        Assert.DoesNotContain("layout.sections.length === 1", shell);
    }

    [Fact]
    public void Editor_OffersAndMapsSectionWidth()
    {
        var view = Read("views/layouts.html");
        Assert.Contains("value: width", view);
        Assert.Contains("value=\"site\"", view);
        Assert.Contains("value=\"full\"", view);
        var model = Read("viewmodels/layouts.js");
        Assert.Contains("width: section.width()", model);
        Assert.Contains("data.width || \"site\"", model);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_TranslateAllWidgetTexts(string locale)
    {
        var sources = Read("models/widgets.js") + Read("components/sitewidget.js") + Read("components/sitewidget.html");
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
        var messages = document.RootElement.GetProperty("messages");
        var keys = Regex.Matches(sources, "[\"'](widgets\\.[A-Za-z0-9.]+)[\"']")
            .Select(match => match.Groups[1].Value).Distinct();
        foreach (var key in keys)
        {
            Assert.True(messages.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()), key);
        }
    }
}
