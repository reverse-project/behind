using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed record SiteDraft(string Title, string Slug, string Content, bool Publish, bool HideTitle);

public static class SiteDraftExtensions
{
    extension(SiteEndpoints.SaveSiteRequest request)
    {
        public Option<SiteDraft> ToDraft()
        {
            var title = (request.Title ?? string.Empty).Trim();
            var slugSource = string.IsNullOrWhiteSpace(request.Slug) ? title : request.Slug;

            return title.Length == 0
                ? None
                : slugSource.ToSlug(Site.MaxSlugLength)
                    .Map(slug => new SiteDraft(title, slug, (request.Content ?? string.Empty).SanitizeRichText(), request.Publish, request.HideTitle));
        }
    }
}
