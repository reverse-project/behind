using Behind.Common.WebApi;
using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class ArticlesFrontendTests
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
    [InlineData("controllers/articles.js")]
    [InlineData("controllers/articlereader.js")]
    [InlineData("controllers/articlemanager.js")]
    [InlineData("controllers/articleeditor.js")]
    [InlineData("controllers/articlecategories.js")]
    [InlineData("viewmodels/articles.js")]
    [InlineData("viewmodels/articlereader.js")]
    [InlineData("viewmodels/articlemanager.js")]
    [InlineData("viewmodels/articleeditor.js")]
    [InlineData("viewmodels/articlecategories.js")]
    [InlineData("views/articles.html")]
    [InlineData("views/articlereader.html")]
    [InlineData("views/articlemanager.html")]
    [InlineData("views/articleeditor.html")]
    [InlineData("views/articlecategories.html")]
    [InlineData("models/articles.js")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Theory]
    [InlineData("articles")]
    [InlineData("articlereader")]
    [InlineData("articlemanager")]
    [InlineData("articleeditor")]
    [InlineData("articlecategories")]
    public void Controllers_BindTheirViewAndViewModel(string name)
    {
        var controller = Read($"controllers/{name}.js");

        Assert.Contains($"text!views/{name}.html", controller);
        Assert.Contains($"viewmodels/{name}", controller);
        Assert.Contains("createController(view,", controller);
    }

    [Fact]
    public void Routes_ServeTheArticlePagesBeforeTheSiteSlugs()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/articles\": \"controllers/articles\"", app);
        Assert.Contains("\"#/articles/:id\": \"controllers/articlereader\"", app);
        Assert.Contains("\"#/manage/articles\": \"controllers/articlemanager\"", app);
        Assert.Contains("\"#/manage/articles/new\": \"controllers/articleeditor\"", app);
        Assert.Contains("\"#/manage/articles/:id\": \"controllers/articleeditor\"", app);
        Assert.Contains("\"#/manage/article-categories\": \"controllers/articlecategories\"", app);
        Assert.True(
            app.IndexOf("\"#/manage/articles/new\"", StringComparison.Ordinal)
            < app.IndexOf("\"#/manage/articles/:id\"", StringComparison.Ordinal));
        Assert.True(
            app.IndexOf("\"#/articles/:id\"", StringComparison.Ordinal)
            < app.IndexOf("\"#/:slug\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Navigation_LinksToTheArticlesPageFromTheBuiltInMenuAndOffersAdministrationToArticleReaders()
    {
        Assert.True(BuiltInMenus.Main.HasItem("Articles", "#/articles", MenuItemKind.Link));
        var administration = Read("viewmodels/administration.js");
        Assert.Contains("href: \"#/manage/articles\", allowed: canReadArticles", administration);
        Assert.Contains("href: \"#/manage/article-categories\", allowed: canReadArticles", administration);
        Assert.Contains("session.hasGrant(\"Article.Read\")", administration);
    }

    [Fact]
    public void ArticlesModel_CallsTheArticleEndpoints()
    {
        var model = Read("models/articles.js");

        Assert.Contains("\"/articles?\" + paging(query)", model);
        Assert.Contains("\"&search=\" + encodeURIComponent(query.search)", model);
        Assert.Contains("\"&category=\" + encodeURIComponent(query.category)", model);
        Assert.Contains("managing(\"/articles/manage\", query)", model);
        Assert.Contains("managing(\"/articles/categories/manage\", query)", model);
        Assert.Contains("http.get(\"/articles/\" + id)", model);
        Assert.Contains("http.post(\"/articles\", draft(fields))", model);
        Assert.Contains("http.get(\"/articles/\" + id + \"/versions\")", model);
        Assert.Contains("http.get(\"/articles/\" + id + \"/versions/\" + version)", model);
        Assert.Contains("http.post(\"/articles/\" + id + \"/versions\", draft(fields))", model);
        Assert.Contains("http.post(\"/articles/\" + id + \"/versions/\" + version + \"/activate\")", model);
        Assert.Contains("http.post(\"/articles/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/articles/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/articles/\" + id + \"/hard/delete\")", model);
        Assert.Contains("http.get(\"/articles/categories\")", model);
        Assert.Contains("http.post(\"/articles/categories\", categoryDraft(fields))", model);
        Assert.Contains("http.post(\"/articles/categories/\" + id, categoryDraft(fields))", model);
        Assert.Contains("http.post(\"/articles/categories/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/articles/categories/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/articles/categories/\" + id + \"/hard/delete\")", model);
        Assert.Contains("http.postForm(\"/articles/images\", form)", model);
        Assert.Contains("models/results", model);
        Assert.Contains("name-taken", model);
        Assert.Contains("version-conflict", model);
        Assert.Contains("unknown-category", model);
        Assert.Contains("unknown-image", model);
    }

    [Fact]
    public void ArticlesModel_HasNoScheduleHighlightOrComments()
    {
        var model = Read("models/articles.js");

        Assert.DoesNotContain("startAt", model);
        Assert.DoesNotContain("endAt", model);
        Assert.DoesNotContain("highlighted", model);
        Assert.DoesNotContain("invalid-schedule", model);
        Assert.DoesNotContain("comments", model);
    }

    [Fact]
    public void ArticlesView_ListsPaginatedArticlesFilteredByCategoryAndSearch()
    {
        var view = Read("views/articles.html");
        var viewModel = Read("viewmodels/articles.js");

        Assert.All(
            new[]
            {
                "submit: find", "value: search", "type=\"search\"", "foreach: filters", "click: select", "foreach: items",
                "html: summary", "text: author", "text: stamp", "text: categoryName", "attr: { href: href }", "<pager",
                "visible: hasBanner"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("category: self.selected()", viewModel);
        Assert.Contains("search: self.search().trim()", viewModel);
        Assert.Contains("context.params.search", viewModel);
        Assert.Contains("articles.list(", viewModel);
        Assert.Contains("self.pager.page(1)", viewModel);
        Assert.Contains("createPager", viewModel);
        Assert.Contains("href: \"#/articles/\" + item.id", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void ArticleReaderView_ShowsTheContentAuthorAndTime()
    {
        var view = Read("views/articlereader.html");
        var viewModel = Read("viewmodels/articlereader.js");

        Assert.All(
            new[] { "html: content", "text: author", "text: stamp", "visible: missing", "visible: found", "href=\"#/articles\"" },
            binding => Assert.Contains(binding, view));
        Assert.Contains("articles.get(articleId)", viewModel);
        Assert.Contains("self.content(result.data.displayContent)", viewModel);
        Assert.DoesNotContain("comment", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void ArticleManagerView_ListsArticlesWithSoftDeleteRestoreAndHardDelete()
    {
        var view = Read("views/articlemanager.html");
        var viewModel = Read("viewmodels/articlemanager.js");

        Assert.All(
            new[]
            {
                "foreach: items", "href=\"#/manage/articles/new\"", "href=\"#/manage/article-categories\"", "visible: canWrite",
                "visible: isDenied", "submit: find", "value: search", "checked: showDeleted", "<letter-pager", "<pager",
                "click: remove", "click: restore", "click: hardDelete", "visible: isDraft && !isDeleted",
                "visible: isPublished && !isDeleted", "visible: hasNewerDraft && !isDeleted"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("createManagedList", viewModel);
        Assert.Contains("session.hasGrant(\"Article.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"Article.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"Article.Delete\")", viewModel);
        Assert.Contains("articles.restore(item.id)", viewModel);
        Assert.Contains("articles.hardDelete(item.id)", viewModel);
        Assert.Contains("return data.articles;", viewModel);
        Assert.DoesNotContain("isScheduled", view);
        Assert.DoesNotContain("isExpired", view);
        Assert.DoesNotContain("isHighlighted", view);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void ArticleEditorView_EditsTitleCategoryAndBothRichTextsWithoutScheduleOrHighlight()
    {
        var view = Read("views/articleeditor.html");
        var viewModel = Read("viewmodels/articleeditor.js");

        Assert.All(
            new[]
            {
                "value: title", "value: categoryId", "options: categories", "richEditor: { value: summary, upload: uploadImage",
                "richEditor: { value: content, upload: uploadImage", "submit: saveDraft", "click: publish", "foreach: versions",
                "click: load", "click: activate", "visible: isDenied", "visible: canRead"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("session.hasGrant(\"Article.Write\")", viewModel);
        Assert.Contains("articles.uploadImage(file)", viewModel);
        Assert.Contains("articles.history(articleId)", viewModel);
        Assert.Contains("articles.saveVersion(articleId, fields)", viewModel);
        Assert.Contains("context.navigate(\"#/manage/articles/\" + result.data.id)", viewModel);
        Assert.DoesNotContain("datetime-local", view);
        Assert.DoesNotContain("scheduled", view);
        Assert.DoesNotContain("highlighted", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("startAt", viewModel);
        Assert.DoesNotContain("highlighted", viewModel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void ArticleCategoriesView_UploadsHeaderImagesAndOffersTheDeletedRecordsFlow()
    {
        var view = Read("views/articlecategories.html");
        var viewModel = Read("viewmodels/articlecategories.js");

        Assert.All(
            new[]
            {
                "value: name", "type=\"file\"", "event: { change: chooseImage }", "submit: save", "click: removeImage",
                "foreach: items", "click: edit", "click: remove", "click: restore", "click: hardDelete", "checked: showDeleted",
                "<letter-pager", "<pager", "href=\"#/manage/articles\""
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("articles.uploadImage(file)", viewModel);
        Assert.Contains("articles.manageCategories", viewModel);
        Assert.Contains("articles.restoreCategory(row.id)", viewModel);
        Assert.Contains("articles.hardDeleteCategory(row.id)", viewModel);
        Assert.Contains("createManagedList", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Theory]
    [InlineData("models/articles.js")]
    [InlineData("viewmodels/articles.js")]
    [InlineData("viewmodels/articlereader.js")]
    [InlineData("viewmodels/articlemanager.js")]
    [InlineData("viewmodels/articleeditor.js")]
    [InlineData("viewmodels/articlecategories.js")]
    public void Scripts_HaveNoComments(string path)
    {
        var script = Read(path);

        Assert.DoesNotContain("//", script);
        Assert.DoesNotContain("/*", script);
    }
}
