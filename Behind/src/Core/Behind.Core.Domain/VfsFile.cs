using Behind.Common;

namespace Behind.Core.Domain;

public sealed record VfsFile(
    VersionedKey<Guid> Id,
    string Type,
    string FileName,
    string ContentType,
    long Length,
    Guid? ParentId)
{
    public const string EntityType = nameof(VfsFile);
    public const int MaxTypeLength = 100;
    public const int MaxFileNameLength = 200;
    public const int MaxContentTypeLength = 100;

    public VfsFile(string type, string fileName, string contentType, long length, Guid? parentId = null)
        : this(new VersionedKey<Guid>(Guid.Empty), type, fileName, contentType, length, parentId)
    {
    }
}

public static class VfsTypes
{
    public const string Sites = "Sites";
    public const string News = "News";
    public const string Articles = "Articles";
    public const string NewsCategories = "NewsCategories";
    public const string ArticleCategories = "ArticleCategories";
    public const string Galleries = "Galleries";
    public const string SiteImageUrl = "/sites/images";

    private static readonly IReadOnlyList<string> SiteImageTypes =
        [Sites, News, Articles, NewsCategories, ArticleCategories];

    private static readonly IReadOnlyList<string> GalleryImageTypes = [Galleries];

    public static IReadOnlyList<string> SameKindAs(string type) =>
        type == Galleries ? GalleryImageTypes : SiteImageTypes;
}
