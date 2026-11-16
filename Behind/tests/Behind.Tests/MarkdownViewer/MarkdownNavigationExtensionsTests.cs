using Behind.MarkdownViewer.Domain;
using Behind.MarkdownViewer.WebApi.Markdown;

namespace Behind.Tests.MarkdownViewer;

public class MarkdownNavigationExtensionsTests
{
    private static MarkdownSourceDocument Document(string path, string title) => new(path, $"# {title}");

    private static readonly MarkdownSourceDocument Home = new(
        "README.md",
        "# Behind Documentation\n\n- **[Developer documentation](./developer/README.md)** - code\n- **[User manual](./user-manual/README.md)** - usage");

    private static readonly IReadOnlyList<MarkdownSourceDocument> Documents =
    [
        Document("developer/CODING.md", "Coding"),
        Document("developer/README.md", "Behind Codebase Documentation"),
        Home,
        Document("user-manual/README.md", "Behind User Manual"),
        Document("user-manual/admin/users.md", "Users"),
        Document("user-manual/news.md", "News")
    ];

    [Fact]
    public void ToNavigation_ExcludesTheHomeFromTheSectionsAndReportsIt()
    {
        var navigation = Documents.ToNavigation("README.md");

        Assert.Equal("README.md", navigation.Home);
        Assert.Equal(6, navigation.Entries.Count);
        Assert.DoesNotContain(navigation.Sections.SelectMany(section => section.Entries), entry => entry.Path == "README.md");
        Assert.DoesNotContain(navigation.Sections, section => section.Title == "Behind Documentation");
    }

    [Fact]
    public void ToNavigation_NamesTheSectionsAfterTheLinksOfTheHomeDocument()
    {
        var sections = Documents.ToNavigation("README.md").Sections;

        Assert.Equal(["Developer documentation", "User manual"], sections.Select(section => section.Title));
        Assert.Equal(["developer/README.md", "user-manual/README.md"], sections.Select(section => section.Path));
    }

    [Fact]
    public void ToNavigation_ListsTheDocumentsOfASectionWithoutItsIndex()
    {
        var sections = Documents.ToNavigation("README.md").Sections;

        Assert.Equal(["developer/CODING.md"], sections[0].Entries.Select(entry => entry.Path));
        Assert.Equal(["user-manual/admin/users.md", "user-manual/news.md"], sections[1].Entries.Select(entry => entry.Path));
    }

    [Fact]
    public void ToNavigation_UsesTheTitleOfTheIndexWhenTheHomeDoesNotLinkIt()
    {
        var documents = new[] { Home with { Markdown = "# Home" }, Document("guide/README.md", "The guide"), Document("guide/a.md", "A") };

        var sections = documents.ToNavigation("README.md").Sections;

        Assert.Equal("The guide", sections.Single().Title);
        Assert.Equal("guide/README.md", sections.Single().Path);
    }

    [Theory]
    [InlineData("user-manual", "User manual")]
    [InlineData("api_docs", "Api docs")]
    [InlineData("guide", "Guide")]
    public void ToNavigation_NamesAFolderWithoutAnIndexAfterTheFolder(string folder, string title)
    {
        var sections = new[] { Document($"{folder}/a.md", "A") }.ToNavigation("README.md").Sections;

        Assert.Equal(new MarkdownSection(title, null, [new MarkdownEntry($"{folder}/a.md", "A")]), sections.Single(), SectionComparer.Instance);
    }

    [Fact]
    public void ToNavigation_PutsTheRootDocumentsInAnUntitledFirstSection()
    {
        var documents = new[] { Home, Document("faq.md", "FAQ"), Document("guide/a.md", "A") };

        var sections = documents.ToNavigation("README.md").Sections;

        Assert.Equal(["", "Guide"], sections.Select(section => section.Title));
        Assert.Equal(["faq.md"], sections[0].Entries.Select(entry => entry.Path));
    }

    [Fact]
    public void ToNavigation_WithoutAHome_HasNoHomeAndKeepsEveryDocument()
    {
        var navigation = new[] { Document("a.md", "A"), Document("b.md", "B") }.ToNavigation("README.md");

        Assert.Null(navigation.Home);
        Assert.Equal(["a.md", "b.md"], navigation.Sections.Single().Entries.Select(entry => entry.Path));
    }

    [Fact]
    public void LinkLabels_ReadsTheTextOfTheRelativeLinks()
    {
        var labels = "[One](./a/README.md) [Two](b/c.md#part) ![img](x.png) [Web](https://example.com/)".LinkLabels();

        Assert.Equal("One", labels["a/README.md"]);
        Assert.Equal("Two", labels["b/c.md"]);
        Assert.False(labels.ContainsKey("x.png"));
        Assert.Equal("Web", labels["https://example.com/"]);
    }

    private sealed class SectionComparer : IEqualityComparer<MarkdownSection>
    {
        public static SectionComparer Instance { get; } = new();

        public bool Equals(MarkdownSection? x, MarkdownSection? y) =>
            x is not null && y is not null && x.Title == y.Title && x.Path == y.Path && x.Entries.SequenceEqual(y.Entries);

        public int GetHashCode(MarkdownSection obj) => obj.Title.GetHashCode();
    }
}
