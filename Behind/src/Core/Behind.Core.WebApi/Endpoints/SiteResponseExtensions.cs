using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Site = Behind.Core.Domain.Site;

namespace Behind.Core.WebApi.Endpoints;

public static class SiteResponseExtensions
{
    public const string SlugTakenReason = "slug-taken";
    public const string VersionConflictReason = "version-conflict";

    extension(ISiteQueries.SiteListing listing)
    {
        public SiteListItem ToItem(DeletionState state) =>
            new(
                listing.Id,
                listing.Title,
                listing.Slug,
                listing.LatestVersion,
                listing.ActiveVersion.ToNullable(),
                listing.ActiveSlug.Match<string?>(() => null, slug => slug),
                listing.UpdatedAt,
                state == DeletionState.Deleted);
    }

    extension(SitesConfiguration configuration)
    {
        public Option<string> IndexSlugOption => configuration.IndexSlug.ToSlug(Site.MaxSlugLength);

        public SiteSettingsResponse ToResponse() =>
            new(configuration.IndexSlugOption.Match<string?>(() => null, slug => slug), configuration.ImageLimit, Site.MaxSections);
    }

    extension(ISiteQueries.SiteHistory history)
    {
        public SiteHistoryResponse ToResponse() =>
            new(
                history.Id,
                history.ActiveVersion.ToNullable(),
                history.Versions.Select(version => version.ToItem()).ToList());
    }

    extension(ISiteQueries.SiteVersionInfo version)
    {
        private SiteVersionItem ToItem() =>
            new(
                version.Version,
                version.Title,
                version.Slug,
                version.PublishedAt.ToNullable(),
                version.CreatedAt,
                version.IsActive,
                version.PublishedAt.Match(() => true, _ => false));
    }

    extension(ISiteQueries.SiteSectionInfo section)
    {
        public SiteSectionResponse ToResponse() =>
            new(
                section.Id,
                section.Theme,
                section.Title,
                section.Content,
                section.ImageId.ToNullable(),
                section.ImageId.Match<string?>(() => null, imageId => $"{VfsTypes.SiteImageUrl}/{imageId}"),
                section.ImageAlt,
                section.ImagePosition);
    }

    extension(Site site)
    {
        public SiteVersionResponse ToVersionResponse(IReadOnlyList<ISiteQueries.SiteSectionInfo> sections) =>
            new(
                site.Id.Key,
                site.Id.Version,
                site.Title,
                site.Slug,
                site.Content,
                site.PublishedAt.ToNullable(),
                site.CreatedAt,
                site.IsDraft,
                site.HideTitle,
                sections.Select(section => section.ToResponse()).ToList());

        public Option<PublicSiteResponse> ToPublicResponse(IReadOnlyList<ISiteQueries.SiteSectionInfo> sections) =>
            site.PublishedAt.Map(publishedAt => new PublicSiteResponse(
                site.Id.Key,
                site.Title,
                site.Slug,
                site.Content,
                publishedAt,
                site.HideTitle,
                sections.Select(section => section.ToResponse()).ToList()));
    }

    extension(SiteSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                SiteSaveResult.Saved saved => Results.Json(new SiteSavedResponse(saved.Id.Key, saved.Id.Version)),
                SiteSaveResult.NotFound => Results.NotFound(),
                SiteSaveResult.UnknownImage => Results.BadRequest(),
                SiteSaveResult.SlugTaken => Results.Conflict(new SiteConflictResponse(SlugTakenReason)),
                _ => Results.Conflict(new SiteConflictResponse(VersionConflictReason))
            };
    }
}
