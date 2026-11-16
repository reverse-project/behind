using Behind.Core.Database;

namespace Behind.Tests.Domain;

public class ArticleTests
{
    [Fact]
    public void Article_KeepsItsFields()
    {
        var id = new VersionedKey<Guid>(Guid.NewGuid(), 2);

        var article = new Article(id, "Title", "<p>short</p>", "<p>long</p>");

        Assert.Equal((id, "Title", "<p>short</p>", "<p>long</p>"), (article.Id, article.Title, article.Summary, article.Content));
        Assert.Equal(0, new Article("Title", "", "").Id.Version);
        Assert.Equal(nameof(Article), Article.EntityType);
    }

    [Fact]
    public void ArticleCategory_KeepsItsName()
    {
        var id = new VersionedKey<Guid>(Guid.NewGuid(), 1);

        Assert.Equal(("Tutorials", id), (new ArticleCategory(id, "Tutorials").Name, new ArticleCategory(id, "Tutorials").Id));
        Assert.Equal(0, new ArticleCategory("Tutorials").Id.Version);
        Assert.Equal(nameof(ArticleCategory), ArticleCategory.EntityType);
    }

    [Fact]
    public void GrantArea_ListsTheArticleArea()
    {
        Assert.Contains(GrantArea.Article, GrantArea.All);
        Assert.Contains("Article.Read", BuiltInAccess.GrantNames);
    }

    [Fact]
    public void ArticleDetail_DisplaysTheSummaryWhenThereIsNoContent()
    {
        var detail = new IArticleQueries.ArticleDetail(new VersionedKey<Guid>(Guid.NewGuid(), 1), "T", "<p>s</p>", " ", "A", None, None, None);

        Assert.Equal("<p>s</p>", detail.DisplayContent);
        Assert.Equal("<p>c</p>", (detail with { Content = "<p>c</p>" }).DisplayContent);
    }
}
