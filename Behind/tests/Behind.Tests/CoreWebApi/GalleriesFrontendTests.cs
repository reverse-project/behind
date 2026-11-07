using Behind.Common.WebApi;
using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class GalleriesFrontendTests
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
    [InlineData("controllers/galleries.js")]
    [InlineData("controllers/galleryviewer.js")]
    [InlineData("controllers/gallerymanager.js")]
    [InlineData("controllers/galleryeditor.js")]
    [InlineData("viewmodels/galleries.js")]
    [InlineData("viewmodels/galleryviewer.js")]
    [InlineData("viewmodels/gallerymanager.js")]
    [InlineData("viewmodels/galleryeditor.js")]
    [InlineData("views/galleries.html")]
    [InlineData("views/galleryviewer.html")]
    [InlineData("views/gallerymanager.html")]
    [InlineData("views/galleryeditor.html")]
    [InlineData("models/galleries.js")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Theory]
    [InlineData("galleries")]
    [InlineData("galleryviewer")]
    [InlineData("gallerymanager")]
    [InlineData("galleryeditor")]
    public void Controllers_BindTheirViewAndViewModel(string name)
    {
        var controller = Read($"controllers/{name}.js");

        Assert.Contains($"text!views/{name}.html", controller);
        Assert.Contains($"viewmodels/{name}", controller);
        Assert.Contains("createController(view,", controller);
    }

    [Fact]
    public void Routes_ServeTheGalleryPagesBeforeTheSiteSlugs()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/galleries\": \"controllers/galleries\"", app);
        Assert.Contains("\"#/galleries/:id\": \"controllers/galleryviewer\"", app);
        Assert.Contains("\"#/manage/galleries\": \"controllers/gallerymanager\"", app);
        Assert.Contains("\"#/manage/galleries/new\": \"controllers/galleryeditor\"", app);
        Assert.Contains("\"#/manage/galleries/:id\": \"controllers/galleryeditor\"", app);
        Assert.True(
            app.IndexOf("\"#/manage/galleries/new\"", StringComparison.Ordinal)
            < app.IndexOf("\"#/manage/galleries/:id\"", StringComparison.Ordinal));
        Assert.True(
            app.IndexOf("\"#/galleries/:id\"", StringComparison.Ordinal)
            < app.IndexOf("\"#/:slug\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Navigation_LinksToTheGalleriesPageFromTheBuiltInMenuAndOffersAdministrationToGalleryReaders()
    {
        Assert.True(BuiltInMenus.Main.HasItem("Galleries", "#/galleries", MenuItemKind.Link));
        var administration = Read("viewmodels/administration.js");
        Assert.Contains("href: \"#/manage/galleries\", allowed: canReadGalleries", administration);
        Assert.Contains("session.hasGrant(\"Gallery.Read\")", administration);
    }

    [Fact]
    public void GalleriesModel_CallsTheGalleryEndpoints()
    {
        var model = Read("models/galleries.js");

        Assert.Contains("\"/galleries?\" + paging(query)", model);
        Assert.Contains("\"&search=\" + encodeURIComponent(query.search)", model);
        Assert.Contains("managing(\"/galleries/manage\", query)", model);
        Assert.Contains("http.get(\"/galleries/\" + id)", model);
        Assert.Contains("http.post(\"/galleries\", draft(fields))", model);
        Assert.Contains("http.get(\"/galleries/\" + id + \"/versions\")", model);
        Assert.Contains("http.get(\"/galleries/\" + id + \"/versions/\" + version)", model);
        Assert.Contains("http.post(\"/galleries/\" + id + \"/versions\", draft(fields))", model);
        Assert.Contains("http.post(\"/galleries/\" + id + \"/versions/\" + version + \"/activate\")", model);
        Assert.Contains("http.post(\"/galleries/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/galleries/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/galleries/\" + id + \"/hard/delete\")", model);
        Assert.Contains("http.postForm(\"/galleries/images\", form)", model);
        Assert.Contains("http.postForm(\"/galleries/thumbnail\", form)", model);
        Assert.Contains("form.append(\"files\", file)", model);
        Assert.Contains("models/results", model);
        Assert.DoesNotContain("missing-descriptions", model);
        Assert.Contains("version-conflict", model);
        Assert.Contains("unknown-image", model);
    }

    [Fact]
    public void GalleriesModel_SendsTheItemsInOrderWithTheirDescriptions()
    {
        var model = Read("models/galleries.js");

        Assert.Contains("thumbnailImageId: fields.thumbnailImageId || null", model);
        Assert.Contains("imageId: item.imageId", model);
        Assert.Contains("thumbnailId: item.thumbnailId", model);
        Assert.Contains("description: item.description", model);
        Assert.Contains("publish: !!fields.publish", model);
    }

    [Fact]
    public void GalleriesModel_UploadsInBatchesThatStayWithinTheServerLimit()
    {
        var model = Read("models/galleries.js");
        var batch = int.Parse(
            System.Text.RegularExpressions.Regex.Match(model, @"uploadBatchSize: (\d+)").Groups[1].Value);

        Assert.InRange(batch, 1, Behind.Core.WebApi.Endpoints.GalleryImageEndpoints.MaxUploadFiles);
    }

    [Fact]
    public void GalleriesView_ListsPaginatedGalleriesWithThumbnailsAndSearch()
    {
        var view = Read("views/galleries.html");
        var viewModel = Read("viewmodels/galleries.js");

        Assert.All(
            new[]
            {
                "submit: find", "value: search", "type=\"search\"", "foreach: items", "text: description", "text: author",
                "text: stamp", "text: count", "attr: { src: thumbnailUrl }", "visible: hasThumbnail", "<pager",
                "visible: isEmpty", "loading=\"lazy\""
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("search: self.search().trim()", viewModel);
        Assert.Contains("context.params.search", viewModel);
        Assert.Contains("galleries.list(", viewModel);
        Assert.Contains("href: \"#/galleries/\" + item.id", viewModel);
        Assert.Contains("createPager", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void GalleryViewerView_ShowsTheThumbnailGridAndALargeViewerWithDescriptions()
    {
        var view = Read("views/galleryviewer.html");
        var viewModel = Read("viewmodels/galleryviewer.js");

        Assert.All(
            new[]
            {
                "foreach: images", "click: open", "attr: { src: thumbnailUrl, alt: description }", "text: description",
                "visible: isOpen", "hasFocus: lightboxFocus", "event: { keydown: press }", "click: close",
                "click: previous", "click: next", "attr: { src: imageUrl, alt: description }", "text: position",
                "visible: hasCaption", "text: caption", "visible: missing", "visible: found", "href=\"#/galleries\""
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("galleries.get(galleryId)", viewModel);
        Assert.Contains("\"Escape\"", viewModel);
        Assert.Contains("\"ArrowLeft\"", viewModel);
        Assert.Contains("\"ArrowRight\"", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void GalleryManagerView_ListsGalleriesWithSoftDeleteRestoreAndHardDelete()
    {
        var view = Read("views/gallerymanager.html");
        var viewModel = Read("viewmodels/gallerymanager.js");

        Assert.All(
            new[]
            {
                "foreach: items", "href=\"#/manage/galleries/new\"", "visible: canWrite", "visible: isDenied",
                "submit: find", "value: search", "checked: showDeleted", "<letter-pager", "<pager", "click: remove",
                "click: restore", "click: hardDelete", "visible: isDraft && !isDeleted",
                "visible: isPublished && !isDeleted", "visible: hasNewerDraft && !isDeleted", "text: count",
                "attr: { src: thumbnailUrl }"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("createManagedList", viewModel);
        Assert.Contains("session.hasGrant(\"Gallery.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"Gallery.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"Gallery.Delete\")", viewModel);
        Assert.Contains("galleries.restore(item.id)", viewModel);
        Assert.Contains("galleries.hardDelete(item.id)", viewModel);
        Assert.Contains("return data.galleries;", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void GalleryEditorView_UploadsABatchAndLetsYouDescribeTheImagesBeforePublishing()
    {
        var view = Read("views/galleryeditor.html");
        var viewModel = Read("viewmodels/galleryeditor.js");

        Assert.All(
            new[]
            {
                "value: title", "value: description", "type=\"file\" multiple", "event: { change: addFiles }",
                "event: { change: addThumbnail }", "options: thumbnailSources", "value: thumbnailMode",
                "foreach: items", "value: description, valueUpdate: 'input'",
                "click: moveUp", "click: moveDown", "click: remove", "submit: saveDraft", "click: publish",
                "enable: canSave", "foreach: uploadErrors",
                "text: progress", "foreach: versions", "click: load", "click: activate", "visible: isDenied",
                "visible: canRead", "attr: { src: thumbnailUrl, alt: description() }"
            },
            binding => Assert.Contains(binding, view));
        Assert.DoesNotContain("required", view);
        Assert.Contains("session.hasGrant(\"Gallery.Write\")", viewModel);
        Assert.Contains("galleries.uploadImages(batch)", viewModel);
        Assert.Contains("galleries.uploadThumbnail(file)", viewModel);
        Assert.Contains("galleries.history(galleryId)", viewModel);
        Assert.Contains("galleries.saveVersion(galleryId, fields)", viewModel);
        Assert.Contains("galleries.create(fields)", viewModel);
        Assert.Contains("context.navigate(\"#/manage/galleries/\" + result.data.id)", viewModel);
        Assert.DoesNotContain("canPublish", viewModel);
        Assert.DoesNotContain("hasMissing", viewModel);
        Assert.Contains("thumbnailImageId: self.thumbnailMode() === uploaded ? self.uploadedThumbnailId() : null", viewModel);
        Assert.Contains("batchesOf(files)", viewModel);
        Assert.Contains("self.items.push(item)", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void Changelog_MentionsTheGalleriesUnderTheCurrentVersion()
    {
        using var changelog = JsonDocument.Parse(Read("changelog.json"));

        var latest = changelog.RootElement[0];
        var texts = latest.GetProperty("changes").EnumerateArray().Select(change => change.GetProperty("text").GetString());

        Assert.Contains(texts, text => text!.StartsWith("Galleries:", StringComparison.Ordinal));
    }
}
