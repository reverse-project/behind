using Behind.Common;

namespace Behind.Core.Domain;

public sealed record Article(VersionedKey<Guid> Id, string Title, string Summary, string Content)
{
    public const string EntityType = nameof(Article);
    public const int MaxTitleLength = 200;
    public const int MaxSummaryLength = 20_000;
    public const int MaxContentLength = 1_000_000;

    public Article(string title, string summary, string content)
        : this(new VersionedKey<Guid>(Guid.Empty), title, summary, content)
    {
    }
}

public sealed record ArticleCategory(VersionedKey<Guid> Id, string Name)
{
    public const string EntityType = nameof(ArticleCategory);
    public const int MaxNameLength = 100;

    public ArticleCategory(string name) : this(new VersionedKey<Guid>(Guid.Empty), name)
    {
    }
}
