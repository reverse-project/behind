using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Database;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class NewsDraftExtensions
{
    extension(NewsEndpoints.SaveNewsRequest request)
    {
        public Option<INewsCommands.NewsDraft> ToDraft()
        {
            var title = (request.Title ?? string.Empty).Trim();

            return title.Length == 0
                ? None
                : Some(new INewsCommands.NewsDraft(
                    title,
                    (request.Summary ?? string.Empty).SanitizeRichText(),
                    (request.Content ?? string.Empty).SanitizeRichText(),
                    request.CategoryId.ToOption(),
                    request.Highlighted,
                    request.StartAt.ToOption(),
                    request.EndAt.ToOption()));
        }
    }

    extension(NewsCategoryEndpoints.SaveNewsCategoryRequest request)
    {
        public Option<INewsCategoryCommands.NewsCategoryDraft> ToDraft()
        {
            var name = (request.Name ?? string.Empty).Trim();

            return name.Length == 0
                ? None
                : Some(new INewsCategoryCommands.NewsCategoryDraft(name, request.HeaderImageId.ToOption()));
        }
    }
}
