using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Database;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class ArticleDraftExtensions
{
    extension(ArticleEndpoints.SaveArticleRequest request)
    {
        public Option<IArticleCommands.ArticleDraft> ToDraft()
        {
            var title = (request.Title ?? string.Empty).Trim();

            return title.Length == 0
                ? None
                : Some(new IArticleCommands.ArticleDraft(
                    title,
                    (request.Summary ?? string.Empty).SanitizeRichText(),
                    (request.Content ?? string.Empty).SanitizeRichText(),
                    request.CategoryId.ToOption()));
        }
    }

    extension(ArticleCategoryEndpoints.SaveArticleCategoryRequest request)
    {
        public Option<IArticleCategoryCommands.ArticleCategoryDraft> ToDraft()
        {
            var name = (request.Name ?? string.Empty).Trim();

            return name.Length == 0
                ? None
                : Some(new IArticleCategoryCommands.ArticleCategoryDraft(name, request.HeaderImageId.ToOption()));
        }
    }
}
