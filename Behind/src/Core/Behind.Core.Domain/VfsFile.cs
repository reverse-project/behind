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
    public const string Downloads = "Downloads";
    public const string Carousels = "Carousels";
    public const string AccessSettings = "AccessSettings";
    public const string Layouts = "Layouts";
    public const string SiteImageUrl = "/sites/images";
    public const string NewsImageUrl = "/news/images";
    public const string ArticleImageUrl = "/articles/images";
    public const string CarouselImageUrl = "/carousels/images";
    public const string AccessImageUrl = "/access/images";
    public const string LayoutImageUrl = "/layouts/images";

    public static readonly IReadOnlyList<string> NewsImages = [News, NewsCategories];

    public static readonly IReadOnlyList<string> ArticleImages = [Articles, ArticleCategories];

    public static readonly IReadOnlyList<string> GalleryImages = [Galleries];

    public static readonly IReadOnlyList<string> DownloadFiles = [Downloads];

    public static readonly IReadOnlyList<string> CarouselImages = [Carousels];

    public static readonly IReadOnlyList<string> AccessImages = [AccessSettings];
    public static readonly IReadOnlyList<string> LayoutImages = [Layouts];

    public static readonly IReadOnlyList<string> AllSiteImages = [Sites, .. NewsImages, .. ArticleImages];

    public static IReadOnlyList<string> SameKindAs(string type) =>
        type == Galleries
            ? GalleryImages
            : type == Downloads
                ? DownloadFiles
                : type == Carousels
                    ? CarouselImages
                    : type == AccessSettings
                        ? AccessImages
                        : AllSiteImages.Contains(type)
                            ? AllSiteImages
                            : [type];

    public static string ImageUrlOf(string type) =>
        LayoutImages.Contains(type)
            ? LayoutImageUrl
            : NewsImages.Contains(type)
                ? NewsImageUrl
                : ArticleImages.Contains(type)
                    ? ArticleImageUrl
                    : CarouselImages.Contains(type)
                        ? CarouselImageUrl
                        : AccessImages.Contains(type)
                            ? AccessImageUrl
                            : SiteImageUrl;

    public static IReadOnlyList<string> ReferencedImageUrlsOf(string type) =>
        ImageUrlOf(type) == SiteImageUrl ? [SiteImageUrl] : [ImageUrlOf(type), SiteImageUrl];
}
