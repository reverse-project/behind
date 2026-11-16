using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record Site(
    VersionedKey<Guid> Id,
    string Title,
    string Slug,
    string Content,
    Option<DateTimeOffset> PublishedAt,
    DateTimeOffset CreatedAt,
    bool HideTitle = false)
{
    public const string EntityType = nameof(Site);
    public const int MaxTitleLength = 200;
    public const int MaxSlugLength = 100;
    public const int MaxContentLength = 1_000_000;
    public const int MaxSections = 30;

    public Site(string title, string slug, string content, DateTimeOffset createdAt, bool hideTitle = false)
        : this(new VersionedKey<Guid>(Guid.Empty), title, slug, content, None, createdAt, hideTitle)
    {
    }
}

public static class SiteExtensions
{
    extension(Site site)
    {
        public Site WithId(VersionedKey<Guid> id) => site with { Id = id };

        public Site WithPublishedAt(Option<DateTimeOffset> publishedAt) => site with { PublishedAt = publishedAt };

        public bool IsDraft => site.PublishedAt.Match(() => true, _ => false);
    }
}
