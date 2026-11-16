using Behind.Common.WebApi;
using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class SiteFrontendTests
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
    [InlineData("controllers/home.js")]
    [InlineData("controllers/site.js")]
    [InlineData("controllers/sites.js")]
    [InlineData("controllers/siteeditor.js")]
    [InlineData("viewmodels/site.js")]
    [InlineData("viewmodels/sites.js")]
    [InlineData("viewmodels/siteeditor.js")]
    [InlineData("views/site.html")]
    [InlineData("views/sites.html")]
    [InlineData("views/siteeditor.html")]
    [InlineData("models/sites.js")]
    [InlineData("models/slug.js")]
    [InlineData("models/managedlist.js")]
    [InlineData("bindings/richeditor.js")]
    [InlineData("bindings/richeditorimages.js")]
    [InlineData("bindings/richeditorlinks.js")]
    [InlineData("libraries/quill.min.js")]
    [InlineData("libraries/quill-LICENSE.txt")]
    [InlineData("css/quill.snow.min.css")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Routes_ServeSitesBySlugAfterEveryBuiltInPage()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/\": \"controllers/home\"", app);
        Assert.Contains("\"#/sites\": \"controllers/sites\"", app);
        Assert.Contains("\"#/sites/new\": \"controllers/siteeditor\"", app);
        Assert.Contains("\"#/sites/:id\": \"controllers/siteeditor\"", app);
        Assert.Contains("\"#/site/:slug\": \"controllers/site\"", app);
        Assert.Contains("var siteController = \"controllers/site\"", app);
        Assert.Contains("app.get(aliasRoute,", app);
        var tables = app.IndexOf("var tables = [routes, moduleRouteTable(), siteRoutes];", StringComparison.Ordinal);
        var registration = app.IndexOf("tables.forEach", StringComparison.Ordinal);
        Assert.True(tables >= 0 && registration > tables);
        Assert.True(
            app.IndexOf("\"#/sign/in/magiclink\"", StringComparison.Ordinal)
            < app.IndexOf("app.get(aliasRoute,", StringComparison.Ordinal));
        Assert.True(
            registration < app.IndexOf("app.get(aliasRoute,", StringComparison.Ordinal));
    }

    [Fact]
    public void Header_OffersAdministrationToSiteReadersThroughTheAdministrationPage()
    {
        Assert.Contains("allowed: canReadSites", Read("viewmodels/administration.js"));
    }

    [Fact]
    public void Config_MapsQuillAndIndexLoadsItsTheme()
    {
        Assert.Contains("quill: \"libraries/quill.min\"", Read("config.js"));
        Assert.Contains("css/quill.snow.min.css", Read("index.html"));
    }

    [Theory]
    [InlineData("libraries/quill.min.js")]
    [InlineData("css/quill.snow.min.css")]
    public void QuillAssets_AreSelfHostedWithoutSourceMaps(string path)
    {
        Assert.DoesNotContain("sourceMappingURL", Read(path));
    }

    [Fact]
    public void QuillLicense_IsShippedWithTheEditor()
    {
        Assert.Contains("Slab", Read("libraries/quill-LICENSE.txt"));
        Assert.Contains("Quill Editor v2", Read("css/quill.snow.min.css"));
    }

    [Fact]
    public void SitesModel_CallsTheSiteEndpoints()
    {
        var model = Read("models/sites.js");

        Assert.Contains("var url = \"/sites?page=\"", model);
        Assert.Contains("http.post(\"/sites/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/sites/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/sites/\" + id + \"/hard/delete\")", model);
        Assert.Contains("http.get(\"/sites/\" + id)", model);
        Assert.Contains("http.get(\"/sites/\" + id + \"/versions/\" + version)", model);
        Assert.Contains("http.post(\"/sites\", draft(fields))", model);
        Assert.Contains("http.post(\"/sites/\" + id + \"/versions\", draft(fields))", model);
        Assert.Contains("http.post(\"/sites/\" + id + \"/versions/\" + version + \"/activate\")", model);
        Assert.Contains("http.postForm(\"/sites/images\", form)", model);
        Assert.Contains("http.get(\"/sites/slug/\" + encodeURIComponent(slug))", model);
        Assert.Contains("http.get(\"/sites/index\")", model);
        Assert.Contains("http.get(\"/sites/settings\")", model);
        Assert.Contains("slug-taken", model);
    }

    [Fact]
    public void Http_CanSendMultipartForms()
    {
        var http = Read("models/http.js");

        Assert.Contains("postForm: function (url, form)", http);
        Assert.Contains("body instanceof FormData", http);
        Assert.Contains("processData: !isForm", http);
    }

    [Fact]
    public void Editor_UploadsImagesInsteadOfEmbeddingThem()
    {
        var binding = Read("bindings/richeditor.js");

        Assert.Contains("ko.bindingHandlers.richEditor", binding);
        Assert.Contains("getSemanticHTML", binding);
        Assert.Contains("uploader:", binding);
        Assert.Contains("insertEmbed(position, \"image\", url, \"user\")", binding);
        Assert.Contains("image/webp", binding);
    }

    [Fact]
    public void Editor_LetsImagesBeResizedAndPlaced()
    {
        var binding = Read("bindings/richeditor.js");
        var images = Read("bindings/richeditorimages.js");
        var css = Read("css/app.css");

        Assert.Contains("bindings/richeditorimages", binding);
        Assert.Contains("images.attach(quill)", binding);
        Assert.Contains("Quill.register(\"formats/image\"", images);
        Assert.Contains("imageAlign", images);
        Assert.Contains("ql-image-handle", images);
        Assert.Contains("pointermove", images);
        Assert.All(
            new[] { ".site-content .ql-image-left", ".site-content .ql-image-center", ".site-content .ql-image-right", ".ql-image-handle" },
            selector => Assert.Contains(selector, css));
    }

    [Fact]
    public void Editor_LetsLinksOptionallyOpenInANewTab()
    {
        var binding = Read("bindings/richeditor.js");
        var links = Read("bindings/richeditorlinks.js");
        var css = Read("css/app.css");

        Assert.Contains("bindings/richeditorlinks", binding);
        Assert.Contains("links.register()", binding);
        Assert.Contains("links.attach(quill)", binding);
        Assert.Contains("Quill.register(\"formats/link\"", links);
        Assert.Contains("node.removeAttribute(\"target\")", links);
        Assert.Contains("\"_blank\"", links);
        Assert.Contains("richeditor.linkNewTab", links);
        Assert.Contains(".ql-link-target", css);
    }

    [Fact]
    public void EditorView_OffersSlugEditingDraftsPublishingAndVersions()
    {
        var view = Read("views/siteeditor.html");
        var viewModel = Read("viewmodels/siteeditor.js");

        Assert.All(
            new[]
            {
                "value: title", "value: slugInput", "click: resetSlug", "richEditor: { value: content, upload: uploadImage",
                "submit: saveDraft", "click: publish", "foreach: versions", "click: load", "click: activate",
                "visible: isDenied", "visible: canRead", "checked: hideTitle"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("hideTitle: self.hideTitle()", viewModel);
        Assert.Contains("self.hideTitle(result.data.hideTitle)", viewModel);
        Assert.Contains("self.slugEdited = ko.observable(!self.isNew)", viewModel);
        Assert.Contains("session.hasGrant(\"Site.Write\")", viewModel);
        Assert.Contains("\"#/site/\" + self.live().slug", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void SitesView_ListsSitesWithTheirStatus()
    {
        var view = Read("views/sites.html");

        Assert.All(
            new[]
            {
                "foreach: items", "href=\"#/sites/new\"", "visible: canWrite", "visible: isDenied", "text: status", "submit: find",
                "value: search", "checked: showDeleted", "<letter-pager", "<pager", "click: remove", "click: restore", "click: hardDelete"
            },
            binding => Assert.Contains(binding, view));
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void SiteView_RendersTheActiveContent()
    {
        var view = Read("views/site.html");
        var viewModel = Read("viewmodels/site.js");

        Assert.Contains("html: content", view);
        Assert.Contains("visible: !hideTitle()", view);
        Assert.Contains("self.hideTitle(result.data.hideTitle)", viewModel);
        Assert.Contains("visible: missing", view);
        Assert.Contains("sites.loadPublic(context.params.slug)", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void Slug_MirrorsTheServerRules()
    {
        var slug = Read("models/slug.js");

        Assert.Contains("normalize(\"NFD\")", slug);
        Assert.Contains("maxLength = 100", slug);
        Assert.Contains("create: function (text)", slug);
        Assert.Contains("clean: function (text)", slug);
    }

    [Fact]
    public void Home_ShowsTheCustomIndexSiteOrTheBuiltInWelcome()
    {
        var home = Read("controllers/home.js");

        Assert.Contains("sites.loadIndex()", home);
        Assert.Contains("SiteController : IndexController", home);
        Assert.Contains("loaded: result", home);
        Assert.Contains("context.loaded", Read("viewmodels/site.js"));
    }

    [Fact]
    public void Editor_ShowsWhichSiteIsTheCustomIndexAndTheImageLimit()
    {
        var view = Read("views/siteeditor.html");
        var viewModel = Read("viewmodels/siteeditor.js");

        Assert.Contains("visible: isIndexSlug", view);
        Assert.Contains("visible: isLiveIndex", view);
        Assert.Contains("visible: isIndex", view);
        Assert.Contains("text: imageLimitLabel", view);
        Assert.Contains("sites.settings()", viewModel);
        Assert.Contains("result.data.indexSlug", viewModel);
        Assert.Contains("result.data.maxImageBytes", viewModel);
    }

    [Fact]
    public void SitesList_MarksTheCustomIndexSite()
    {
        Assert.Contains("visible: isIndex", Read("views/sites.html"));
        Assert.Contains("item.activeSlug === indexSlug", Read("viewmodels/sites.js"));
        Assert.Contains("createManagedList", Read("viewmodels/sites.js"));
    }

    [Fact]
    public void ManagedList_SearchesPagesAndSwitchesToDeletedRecordsForDevelopers()
    {
        var list = Read("models/managedlist.js");

        Assert.Contains("models/pager", list);
        Assert.Contains("models/letterpager", list);
        Assert.Contains("session.hasGrant(\"RawDatabase.Manage\")", list);
        Assert.Contains("deleted: list.showDeleted()", list);
        Assert.Contains("list.perform = function", list);
    }
}
