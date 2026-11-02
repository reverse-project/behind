using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class NewsResponseExtensions
{
    public const string UnknownCategoryReason = "unknown-category";
    public const string InvalidScheduleReason = "invalid-schedule";
    public const string VersionConflictReason = "version-conflict";
    public const string NameTakenReason = "name-taken";
    public const string UnknownImageReason = "unknown-image";
    public const string DraftStatus = "draft";

    extension(INewsQueries.NewsCategoryInfo category)
    {
        public NewsCategoryItem ToItem() => category.Id.ToCategoryItem(category.Name, category.HeaderImageId, false);
    }

    extension(INewsCategoryQueries.NewsCategoryListing category)
    {
        public NewsCategoryItem ToItem(DeletionState state) =>
            category.Id.Key.ToCategoryItem(category.Name, category.HeaderImageId, state == DeletionState.Deleted);
    }

    extension(INewsQueries.NewsListing news)
    {
        public NewsListItem ToItem() =>
            new(
                news.Id.Key,
                news.Id.Version,
                news.Title,
                news.Summary,
                news.Author,
                news.CreatedAt.ToNullable(),
                news.UpdatedAt.ToNullable(),
                news.Category.Match<NewsCategoryItem?>(() => null, category => category.ToItem()),
                news.CommentCount,
                news.IsHighlighted,
                news.StartAt.ToNullable(),
                news.EndAt.ToNullable());
    }

    extension(INewsQueries.ManagedNewsListing news)
    {
        public ManagedNewsItem ToItem(DeletionState state, DateTimeOffset now) =>
            new(
                news.Id,
                news.LatestVersion,
                news.ActiveVersion.ToNullable(),
                news.Title,
                news.Author,
                news.CreatedAt.ToNullable(),
                news.UpdatedAt.ToNullable(),
                news.Category.Match<NewsCategoryItem?>(() => null, category => category.ToItem()),
                news.CommentCount,
                state == DeletionState.Deleted,
                news.Live.Match(() => false, live => live.Highlighted),
                news.Live.Match(
                    () => DraftStatus,
                    live => live.StartAt.StatusAt(live.EndAt, now).ToString().ToLowerInvariant()),
                news.ActiveVersion.Match(() => false, version => version < news.LatestVersion),
                news.Live.Bind(live => live.StartAt).ToNullable(),
                news.Live.Bind(live => live.EndAt).ToNullable());
    }

    extension(INewsQueries.NewsDetail news)
    {
        public NewsDetailResponse ToResponse() =>
            new(
                news.Id.Key,
                news.Id.Version,
                news.Title,
                news.Summary,
                news.Content,
                news.DisplayContent,
                news.Author,
                news.CreatedAt.ToNullable(),
                news.UpdatedAt.ToNullable(),
                news.Category.Match<NewsCategoryItem?>(() => null, category => category.ToItem()),
                news.IsHighlighted,
                news.StartAt.ToNullable(),
                news.EndAt.ToNullable());
    }

    extension(INewsQueries.NewsHistory history)
    {
        public NewsHistoryResponse ToResponse() =>
            new(
                history.Id,
                history.ActiveVersion.ToNullable(),
                history.Versions
                    .Select(version => new NewsVersionItem(
                        version.Version,
                        version.Title,
                        version.StartAt.ToNullable(),
                        version.EndAt.ToNullable(),
                        version.Highlighted,
                        version.CreatedAt.ToNullable(),
                        version.IsActive))
                    .ToList());
    }

    extension(INewsCommentQueries.NewsCommentListing comment)
    {
        public NewsCommentItem ToItem(Option<Guid> viewer, bool canDeleteAny, bool canEditAny)
        {
            var isAuthor = viewer.Match(() => false, viewerId => viewerId == comment.AuthorId);

            return new NewsCommentItem(
                comment.Id.Key,
                comment.Author,
                comment.Content,
                comment.CreatedAt.ToNullable(),
                canDeleteAny || isAuthor,
                canEditAny || isAuthor,
                comment.UpdatedAt.ToNullable());
        }
    }

    extension(NewsSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                NewsSaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                NewsSaveResult.NotFound => Results.NotFound(),
                NewsSaveResult.UnknownCategory => Results.Conflict(new NewsConflictResponse(UnknownCategoryReason)),
                NewsSaveResult.InvalidSchedule => Results.Conflict(new NewsConflictResponse(InvalidScheduleReason)),
                _ => Results.Conflict(new NewsConflictResponse(VersionConflictReason))
            };
    }

    extension(NewsCategorySaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                NewsCategorySaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                NewsCategorySaveResult.NotFound => Results.NotFound(),
                NewsCategorySaveResult.NameTaken => Results.Conflict(new NewsConflictResponse(NameTakenReason)),
                NewsCategorySaveResult.UnknownImage => Results.Conflict(new NewsConflictResponse(UnknownImageReason)),
                _ => Results.Conflict(new NewsConflictResponse(VersionConflictReason))
            };
    }

    extension(Guid id)
    {
        internal NewsCategoryItem ToCategoryItem(string name, Option<Guid> headerImageId, bool isDeleted) =>
            new(
                id,
                name,
                headerImageId.ToNullable(),
                headerImageId.Match<string?>(() => null, imageId => $"{SiteImageEndpoints.ImagePath}/{imageId}"),
                isDeleted);
    }
}
