using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class ArticleResponseExtensions
{
    public const string PublishedStatus = "published";

    extension(IArticleQueries.ArticleCategoryInfo category)
    {
        public NewsCategoryItem ToItem() => category.Id.ToCategoryItem(category.Name, category.HeaderImageId, false, VfsTypes.ArticleImageUrl);
    }

    extension(IArticleCategoryQueries.ArticleCategoryListing category)
    {
        public NewsCategoryItem ToItem(DeletionState state) =>
            category.Id.Key.ToCategoryItem(
                category.Name,
                category.HeaderImageId,
                state == DeletionState.Deleted,
                VfsTypes.ArticleImageUrl);
    }

    extension(IArticleQueries.ArticleListing article)
    {
        public ArticleListItem ToItem() =>
            new(
                article.Id.Key,
                article.Id.Version,
                article.Title,
                article.Summary,
                article.Author,
                article.CreatedAt.ToNullable(),
                article.UpdatedAt.ToNullable(),
                article.Category.Match<NewsCategoryItem?>(() => null, category => category.ToItem())) { AuthorId = article.AuthorId };
    }

    extension(IArticleQueries.ManagedArticleListing article)
    {
        public ManagedArticleItem ToItem(DeletionState state) =>
            new(
                article.Id,
                article.LatestVersion,
                article.ActiveVersion.ToNullable(),
                article.Title,
                article.Author,
                article.CreatedAt.ToNullable(),
                article.UpdatedAt.ToNullable(),
                article.Category.Match<NewsCategoryItem?>(() => null, category => category.ToItem()),
                state == DeletionState.Deleted,
                article.ActiveVersion.Match(() => NewsResponseExtensions.DraftStatus, _ => PublishedStatus),
                article.ActiveVersion.Match(() => false, version => version < article.LatestVersion)) { AuthorId = article.AuthorId };
    }

    extension(IArticleQueries.ArticleDetail article)
    {
        public ArticleDetailResponse ToResponse() =>
            new(
                article.Id.Key,
                article.Id.Version,
                article.Title,
                article.Summary,
                article.Content,
                article.DisplayContent,
                article.Author,
                article.CreatedAt.ToNullable(),
                article.UpdatedAt.ToNullable(),
                article.Category.Match<NewsCategoryItem?>(() => null, category => category.ToItem())) { AuthorId = article.AuthorId };
    }

    extension(IArticleQueries.ArticleHistory history)
    {
        public ArticleHistoryResponse ToResponse() =>
            new(
                history.Id,
                history.ActiveVersion.ToNullable(),
                history.Versions
                    .Select(version => new ArticleVersionItem(
                        version.Version,
                        version.Title,
                        version.CreatedAt.ToNullable(),
                        version.IsActive))
                    .ToList());
    }

    extension(ArticleSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                ArticleSaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                ArticleSaveResult.NotFound => Results.NotFound(),
                ArticleSaveResult.UnknownCategory => Results.Conflict(
                    new NewsConflictResponse(NewsResponseExtensions.UnknownCategoryReason)),
                _ => Results.Conflict(new NewsConflictResponse(NewsResponseExtensions.VersionConflictReason))
            };
    }

    extension(ArticleCategorySaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                ArticleCategorySaveResult.Saved saved => Results.Json(
                    new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                ArticleCategorySaveResult.NotFound => Results.NotFound(),
                ArticleCategorySaveResult.NameTaken => Results.Conflict(
                    new NewsConflictResponse(NewsResponseExtensions.NameTakenReason)),
                ArticleCategorySaveResult.UnknownImage => Results.Conflict(
                    new NewsConflictResponse(NewsResponseExtensions.UnknownImageReason)),
                _ => Results.Conflict(new NewsConflictResponse(NewsResponseExtensions.VersionConflictReason))
            };
    }
}
