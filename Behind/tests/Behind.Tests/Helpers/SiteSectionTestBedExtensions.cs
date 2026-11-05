using Behind.Core.Database;
using Behind.Core.Database.Queries;

namespace Behind.Tests.Helpers;

public static class SiteSectionTestBedExtensions
{
    public static ISiteCommands.SiteSectionDraft SectionDraft(
        string title = "Welcome",
        string content = "<p>text</p>",
        SiteSectionTheme theme = SiteSectionTheme.Normal,
        Option<Guid> imageId = default,
        string imageAlt = "",
        SiteSectionImagePosition imagePosition = SiteSectionImagePosition.Left) =>
        new(theme, title, content, imageId, imageAlt, imagePosition);

    extension(NewsTestBed bed)
    {
        public async Task<SiteSaveResult> CreateSiteWithSectionsAsync(
            IReadOnlyList<ISiteCommands.SiteSectionDraft> sections,
            string slug = "landing",
            bool publish = true) =>
            await bed.WithSiteCommandsAsync(commands =>
                commands.Create("Landing", slug, "<p/>", publish, false, sections));

        public async Task<SiteSaveResult> SaveSiteSectionsAsync(
            Guid siteId,
            IReadOnlyList<ISiteCommands.SiteSectionDraft> sections,
            string slug = "landing",
            bool publish = true) =>
            await bed.WithSiteCommandsAsync(commands =>
                commands.SaveVersion(siteId, "Landing", slug, "<p/>", publish, false, sections));

        public async Task<IReadOnlyList<ISiteQueries.SiteSectionInfo>> SectionsOfAsync(VersionedKey<Guid> site)
        {
            await using var ctx = bed.Read();
            await using var versions = bed.ReadVersions();

            return await new SiteQueries(ctx, versions).FindSections(site);
        }
    }
}
