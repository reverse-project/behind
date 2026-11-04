using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class NewsFrontendTests
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
    [InlineData("controllers/news.js")]
    [InlineData("controllers/newsarticle.js")]
    [InlineData("controllers/newsmanager.js")]
    [InlineData("controllers/newseditor.js")]
    [InlineData("controllers/newscategories.js")]
    [InlineData("viewmodels/news.js")]
    [InlineData("viewmodels/newsarticle.js")]
    [InlineData("viewmodels/newsmanager.js")]
    [InlineData("viewmodels/newseditor.js")]
    [InlineData("viewmodels/newscategories.js")]
    [InlineData("views/news.html")]
    [InlineData("views/newsarticle.html")]
    [InlineData("views/newsmanager.html")]
    [InlineData("views/newseditor.html")]
    [InlineData("views/newscategories.html")]
    [InlineData("models/news.js")]
    [InlineData("models/newsmeta.js")]
    [InlineData("models/results.js")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Routes_ServeTheNewsPagesBeforeTheSiteSlugs()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/news\": \"controllers/news\"", app);
        Assert.Contains("\"#/news/:id\": \"controllers/newsarticle\"", app);
        Assert.Contains("\"#/manage/news\": \"controllers/newsmanager\"", app);
        Assert.Contains("\"#/manage/news/new\": \"controllers/newseditor\"", app);
        Assert.Contains("\"#/manage/news/:id\": \"controllers/newseditor\"", app);
        Assert.Contains("\"#/manage/news-categories\": \"controllers/newscategories\"", app);
        Assert.True(
            app.IndexOf("\"#/manage/news/new\"", StringComparison.Ordinal)
            < app.IndexOf("\"#/manage/news/:id\"", StringComparison.Ordinal));
        Assert.True(
            app.IndexOf("\"#/news/:id\"", StringComparison.Ordinal)
            < app.IndexOf("\"#/:slug\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Navigation_LinksToTheNewsPageFromTheBuiltInMenuAndOffersAdministrationToNewsReaders()
    {
        Assert.True(BuiltInMenus.Main.HasItem("News", "#/news", MenuItemKind.Link));
        var administration = Read("viewmodels/administration.js");
        Assert.Contains("href: \"#/manage/news\", allowed: canReadNews", administration);
        Assert.Contains("href: \"#/manage/news-categories\", allowed: canReadNews", administration);
    }

    [Fact]
    public void NewsModel_CallsTheNewsEndpoints()
    {
        var model = Read("models/news.js");

        Assert.Contains("var url = \"/news?\"", model);
        Assert.Contains("managing(\"/news/manage\", query)", model);
        Assert.Contains("managing(\"/news/categories/manage\", query)", model);
        Assert.Contains("http.get(\"/news/\" + id)", model);
        Assert.Contains("http.post(\"/news\", draft(fields))", model);
        Assert.Contains("http.get(\"/news/\" + id + \"/versions\")", model);
        Assert.Contains("http.get(\"/news/\" + id + \"/versions/\" + version)", model);
        Assert.Contains("http.post(\"/news/\" + id + \"/versions\", draft(fields))", model);
        Assert.Contains("http.post(\"/news/\" + id + \"/versions/\" + version + \"/activate\")", model);
        Assert.Contains("http.post(\"/news/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/news/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/news/\" + id + \"/hard/delete\")", model);
        Assert.Contains("http.get(\"/news/categories\")", model);
        Assert.Contains("http.post(\"/news/categories\", categoryDraft(fields))", model);
        Assert.Contains("http.post(\"/news/categories/\" + id, categoryDraft(fields))", model);
        Assert.Contains("http.post(\"/news/categories/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/news/categories/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/news/categories/\" + id + \"/hard/delete\")", model);
        Assert.Contains("http.postForm(\"/news/images\", form)", model);
        Assert.Contains("http.get(\"/news/\" + id + \"/comments?\" + paging(query))", model);
        Assert.Contains("http.post(\"/news/\" + id + \"/comments\", { content: content })", model);
        Assert.Contains("http.post(\"/news/comments/\" + commentId + \"/delete\")", model);
        Assert.Contains("name-taken", model);
    }

    [Fact]
    public void ResultsModel_IsSharedBySitesAndNews()
    {
        Assert.Contains("models/results", Read("models/sites.js"));
        Assert.Contains("models/results", Read("models/news.js"));
        Assert.Contains("create: function (conflictMessages)", Read("models/results.js"));
    }

    [Fact]
    public void NewsView_ListsPaginatedNewsFilteredByCategory()
    {
        var view = Read("views/news.html");
        var viewModel = Read("viewmodels/news.js");

        Assert.All(
            new[]
            {
                "foreach: filters", "click: select", "foreach: items", "html: summary", "text: author", "text: stamp",
                "text: categoryName", "text: commentLabel", "attr: { href: href }", "<pager", "visible: hasBanner"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("category: self.selected()", viewModel);
        Assert.Contains("self.pager.page(1)", viewModel);
        Assert.Contains("createPager", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void NewsArticleView_ShowsTheContentAuthorTimeAndComments()
    {
        var view = Read("views/newsarticle.html");
        var viewModel = Read("viewmodels/newsarticle.js");

        Assert.All(
            new[]
            {
                "html: content", "text: author", "text: stamp", "foreach: comments", "click: remove", "visible: canDelete",
                "submit: post", "value: newComment", "<pager", "visible: isSignedIn", "visible: missing"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("news.get(newsId)", viewModel);
        Assert.Contains("self.content(result.data.displayContent)", viewModel);
        Assert.Contains("news.addComment(newsId", viewModel);
        Assert.Contains("news.removeComment(row.id)", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void Highlight_IsShownInTheListTheArticleAndTheManagerAndEditedInTheEditor()
    {
        Assert.Contains("visible: isHighlighted", Read("views/news.html"));
        Assert.Contains("border-warning", Read("views/news.html"));
        Assert.Contains("visible: isHighlighted", Read("views/newsarticle.html"));
        Assert.Contains("visible: isHighlighted", Read("views/newsmanager.html"));
        Assert.Contains("checked: highlighted", Read("views/newseditor.html"));
        Assert.Contains("highlighted: self.highlighted()", Read("viewmodels/newseditor.js"));
        Assert.Contains("self.highlighted(result.data.isHighlighted)", Read("viewmodels/newseditor.js"));
        Assert.Contains("highlighted: !!fields.highlighted", Read("models/news.js"));
        Assert.Contains("highlighted: self.highlighted()", Read("viewmodels/newseditor.js"));
    }

    [Fact]
    public void Schedule_IsEditedInTheEditorAndShownInTheManager()
    {
        var view = Read("views/newseditor.html");
        var viewModel = Read("viewmodels/newseditor.js");

        Assert.Contains("type=\"datetime-local\"", view);
        Assert.Contains("checked: scheduled", view);
        Assert.Contains("visible: scheduled", view);
        Assert.Contains("value: startAt", view);
        Assert.Contains("value: endAt", view);
        Assert.Contains("visible: !hasValidSchedule()", view);
        Assert.Contains("news.history(newsId)", viewModel);
        Assert.Contains("startAt: self.scheduled() ? meta.fromLocalInput(self.startAt()) : null", viewModel);
        Assert.Contains("endAt: self.scheduled() ? meta.fromLocalInput(self.endAt()) : null", viewModel);
        Assert.Contains("self.scheduled(!!result.data.startAt)", viewModel);
        Assert.Contains("if (!self.scheduled())", viewModel);
        Assert.Contains("self.hasValidSchedule()", viewModel);

        Assert.Contains("visible: isScheduled && !isDeleted", Read("views/newsmanager.html"));
        Assert.Contains("visible: isExpired && !isDeleted", Read("views/newsmanager.html"));
        Assert.Contains("text: schedule", Read("views/newsmanager.html"));
        Assert.Contains("visible: isDraft && !isDeleted", Read("views/newsmanager.html"));
        Assert.Contains("visible: hasNewerDraft && !isDeleted", Read("views/newsmanager.html"));

        var model = Read("models/news.js");
        Assert.Contains("startAt: fields.startAt || null", model);
        Assert.Contains("endAt: fields.endAt || null", model);
        Assert.Contains("invalid-schedule", model);
        Assert.Contains("toLocalInput", Read("models/newsmeta.js"));
        Assert.Contains("fromLocalInput", Read("models/newsmeta.js"));
    }

    [Fact]
    public void Comments_CanBeEditedInPlaceByTheirAuthorOrAModerator()
    {
        var view = Read("views/newsarticle.html");
        var viewModel = Read("viewmodels/newsarticle.js");

        Assert.All(
            new[]
            {
                "visible: canEdit && !isEditing()", "click: startEdit", "submit: saveEdit", "value: draft", "click: cancelEdit",
                "visible: isEdited, text: editedAt"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("news.editComment(row.id, row.draft().trim())", viewModel);
        Assert.Contains("canEdit: item.canEdit", viewModel);
        Assert.Contains("http.post(\"/news/comments/\" + commentId, { content: content })", Read("models/news.js"));
    }

    [Fact]
    public void NewsMeta_ShowsUpdatedOrCreatedTimes()
    {
        var meta = Read("models/newsmeta.js");

        Assert.Contains("content.updated", meta);
        Assert.Contains("content.created", meta);
        Assert.Contains("item.createdAt ?", meta);
        Assert.Contains("content.anonymous", meta);
    }

    [Fact]
    public void NewsManagerView_ListsNewsWithSoftDeleteRestoreAndHardDelete()
    {
        var view = Read("views/newsmanager.html");
        var viewModel = Read("viewmodels/newsmanager.js");

        Assert.All(
            new[]
            {
                "foreach: items", "href=\"#/manage/news/new\"", "visible: canWrite", "visible: isDenied", "submit: find", "value: search",
                "checked: showDeleted", "<letter-pager", "<pager", "click: remove", "click: restore", "click: hardDelete"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("createManagedList", viewModel);
        Assert.Contains("session.hasGrant(\"News.Delete\")", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void NewsEditorView_EditsTitleCategoryAndBothRichTexts()
    {
        var view = Read("views/newseditor.html");
        var viewModel = Read("viewmodels/newseditor.js");

        Assert.All(
            new[]
            {
                "value: title", "value: categoryId", "options: categories", "richEditor: { value: summary, upload: uploadImage",
                "richEditor: { value: content, upload: uploadImage", "submit: saveDraft", "click: publish", "foreach: versions", "click: load", "click: activate", "visible: isDenied", "visible: canRead"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("session.hasGrant(\"News.Write\")", viewModel);
        Assert.Contains("news.uploadImage(file)", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void NewsCategoriesView_UploadsHeaderImagesAndOffersTheDeletedRecordsFlow()
    {
        var view = Read("views/newscategories.html");
        var viewModel = Read("viewmodels/newscategories.js");

        Assert.All(
            new[]
            {
                "value: name", "type=\"file\"", "event: { change: chooseImage }", "submit: save", "click: removeImage",
                "foreach: items", "click: edit", "click: remove", "click: restore", "click: hardDelete", "checked: showDeleted",
                "<letter-pager", "<pager"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("news.uploadImage(file)", viewModel);
        Assert.Contains("createManagedList", viewModel);
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void PublicNewsPage_HasALetterPagerThatSendsTheLetterAndResetsWithTheCategoryAndThePage()
    {
        var view = Read("views/news.html");
        var viewModel = Read("viewmodels/news.js");
        var model = Read("models/news.js");

        Assert.Contains("<letter-pager", view);
        Assert.Contains("<pager", view);
        Assert.Contains("params=\"pager: letterPager\"", view);
        Assert.Contains("letter: self.letterPager.selected()", viewModel);
        Assert.Contains("self.letterPager.update(result.data.letters)", viewModel);
        Assert.Contains("self.letterPager.selected(\"\")", viewModel);
        Assert.Contains("\"&letter=\" + encodeURIComponent(query.letter)", model);
    }

    [Fact]
    public void CommentsSwitch_HidesTheCommentsOfTheNewsPagesWhenTheServerTurnsThemOff()
    {
        var model = Read("models/comments.js");
        var article = Read("viewmodels/newsarticle.js");
        var list = Read("viewmodels/news.js");
        var manager = Read("viewmodels/newsmanager.js");

        Assert.Contains("http.get(\"/comments/settings\")", model);
        Assert.Contains("settings.news ? self.refreshComments() : undefined", article);
        Assert.Contains("self.commentsEnabled(settings.news)", article);
        Assert.Contains("self.commentsEnabled(settings.news)", list);
        Assert.Contains("self.commentsEnabled(settings.news)", manager);
        Assert.Contains("visible: commentsEnabled", Read("views/newsarticle.html"));
        Assert.Contains("visible: $root.commentsEnabled, text: commentLabel", Read("views/news.html"));
        Assert.Contains("visible: $root.commentsEnabled", Read("views/newsmanager.html"));
    }
}
