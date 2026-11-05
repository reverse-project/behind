using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed record SiteDraft(
    string Title,
    string Slug,
    string Content,
    bool Publish,
    bool HideTitle,
    IReadOnlyList<ISiteCommands.SiteSectionDraft> Sections);

public static class SiteDraftExtensions
{
    extension(SiteEndpoints.SaveSiteRequest request)
    {
        public Option<SiteDraft> ToDraft()
        {
            var title = (request.Title ?? string.Empty).Trim();
            var slugSource = string.IsNullOrWhiteSpace(request.Slug) ? title : request.Slug;
            var sections = (request.Sections ?? []).Select(section => section.ToDraft()).ToList();

            return title.Length == 0
                   || sections.Count > Site.MaxSections
                   || sections.Any(section => section.IsNone())
                ? None
                : slugSource.ToSlug(Site.MaxSlugLength)
                    .Map(slug => new SiteDraft(
                        title,
                        slug,
                        (request.Content ?? string.Empty).SanitizeRichText(),
                        request.Publish,
                        request.HideTitle,
                        sections.SelectMany(section => section.AsEnumerable()).ToList()));
        }
    }

    extension(SiteEndpoints.SaveSiteSectionRequest section)
    {
        private Option<ISiteCommands.SiteSectionDraft> ToDraft()
        {
            var title = (section.Title ?? string.Empty).Trim();
            var content = (section.Content ?? string.Empty).SanitizeRichText();
            var imageAlt = (section.ImageAlt ?? string.Empty).Trim();
            var isEmpty = title.Length == 0 && content.Length == 0 && section.ImageId is null;

            return isEmpty
                   || section.ImageId == Guid.Empty
                   || !Enum.IsDefined(section.Theme)
                   || !Enum.IsDefined(section.ImagePosition)
                   || title.Length > SiteSection.MaxTitleLength
                   || content.Length > SiteSection.MaxContentLength
                   || imageAlt.Length > SiteSection.MaxImageAltLength
                ? None
                : Some(new ISiteCommands.SiteSectionDraft(
                    section.Theme,
                    title,
                    content,
                    section.ImageId.ToOption(),
                    section.ImageId is null ? string.Empty : imageAlt,
                    section.ImagePosition));
        }
    }
}
