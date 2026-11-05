using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class SiteSectionsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static VersionedKey<Guid> Saved(SiteSaveResult result) =>
        Assert.IsType<SiteSaveResult.Saved>(result).Id;

    private async Task<bool> ExistsAsync(string path) => (await _bed.ContentStore.Read(path)).Match(() => false, _ => true);

    private async Task<int> StoredSectionsAsync()
    {
        await using var ctx = _bed.Read();

        return await ctx.SiteSections.CountAsync();
    }

    [Fact]
    public async Task Create_StoresTheSectionsInOrderWithAllTheirSettings()
    {
        var image = await _bed.AddImageAsync();

        var site = Saved(await _bed.CreateSiteWithSectionsAsync(
        [
            SiteSectionTestBedExtensions.SectionDraft("Hero", "<p>a</p>", SiteSectionTheme.Accented, Some(image), "Team", SiteSectionImagePosition.Right),
            SiteSectionTestBedExtensions.SectionDraft("Plain", "<p>b</p>")
        ]));

        var sections = await _bed.SectionsOfAsync(site);
        Assert.Equal(["Hero", "Plain"], sections.Select(section => section.Title));
        Assert.Equal([0, 1], sections.Select(section => section.Position));
        Assert.Equal([SiteSectionTheme.Accented, SiteSectionTheme.Normal], sections.Select(section => section.Theme));
        Assert.Equal([SiteSectionImagePosition.Right, SiteSectionImagePosition.Left], sections.Select(section => section.ImagePosition));
        Assert.Equal(Some(image), sections[0].ImageId);
        Assert.Equal(None, sections[1].ImageId);
        Assert.Equal("Team", sections[0].ImageAlt);
        Assert.Equal("<p>a</p>", sections[0].Content);
        Assert.NotEqual(sections[0].Id, sections[1].Id);
    }

    [Fact]
    public async Task Create_WithoutSections_StoresNone()
    {
        var site = Saved(await _bed.CreateSiteWithSectionsAsync([]));

        Assert.Empty(await _bed.SectionsOfAsync(site));
        Assert.Equal(0, await StoredSectionsAsync());
    }

    [Fact]
    public async Task SaveVersion_GivesEveryVersionItsOwnSections()
    {
        var first = Saved(await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("One")]));

        var second = Saved(await _bed.SaveSiteSectionsAsync(
            first.Key,
            [SiteSectionTestBedExtensions.SectionDraft("Two"), SiteSectionTestBedExtensions.SectionDraft("Three")]));

        Assert.Equal(["One"], (await _bed.SectionsOfAsync(first)).Select(section => section.Title));
        Assert.Equal(["Two", "Three"], (await _bed.SectionsOfAsync(second)).Select(section => section.Title));
        Assert.Equal(3, await StoredSectionsAsync());
    }

    [Fact]
    public async Task SaveVersion_WithoutSections_DropsThemFromTheNewVersionOnly()
    {
        var first = Saved(await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("One")]));

        var second = Saved(await _bed.SaveSiteSectionsAsync(first.Key, []));

        Assert.Empty(await _bed.SectionsOfAsync(second));
        Assert.Single(await _bed.SectionsOfAsync(first));
    }

    [Fact]
    public async Task Create_MovesTheSectionImagesAndTheImagesOfTheirContentUnderTheSite()
    {
        var header = await _bed.AddImageAsync();
        var inText = await _bed.AddImageAsync();
        var unused = await _bed.AddImageAsync();

        var site = Saved(await _bed.CreateSiteWithSectionsAsync(
        [
            SiteSectionTestBedExtensions.SectionDraft(
                "Hero",
                $"""<img src="{VfsTypes.SiteImageUrl}/{inText}">""",
                imageId: Some(header))
        ]));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, header, site.Key)));
        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, inText, site.Key)));
        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, unused)));
    }

    [Fact]
    public async Task Create_UnknownSectionImage_SavesNothing()
    {
        var result = await _bed.CreateSiteWithSectionsAsync(
        [
            SiteSectionTestBedExtensions.SectionDraft("Hero", imageId: Some(Guid.NewGuid()))
        ]);

        Assert.IsType<SiteSaveResult.UnknownImage>(result);
        await using var ctx = _bed.Read();
        Assert.Equal(0, await ctx.Sites.CountAsync());
        Assert.Equal(0, await ctx.SiteSections.CountAsync());
    }

    [Fact]
    public async Task Create_ImageOfAnotherKind_IsUnknown()
    {
        var slide = await _bed.AddCarouselImageAsync();

        var result = await _bed.CreateSiteWithSectionsAsync(
        [
            SiteSectionTestBedExtensions.SectionDraft("Hero", imageId: Some(slide))
        ]);

        Assert.IsType<SiteSaveResult.UnknownImage>(result);
    }

    [Fact]
    public async Task Create_SameImageInTwoSections_IsAccepted()
    {
        var image = await _bed.AddImageAsync();

        var site = Saved(await _bed.CreateSiteWithSectionsAsync(
        [
            SiteSectionTestBedExtensions.SectionDraft("A", imageId: Some(image)),
            SiteSectionTestBedExtensions.SectionDraft("B", imageId: Some(image))
        ]));

        Assert.Equal(2, (await _bed.SectionsOfAsync(site)).Count);
    }

    [Fact]
    public async Task Create_WithATakenSlug_StoresNoSections()
    {
        await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("One")]);

        var result = await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("Two")]);

        Assert.IsType<SiteSaveResult.SlugTaken>(result);
        Assert.Equal(1, await StoredSectionsAsync());
    }

    [Fact]
    public async Task FindSections_OfAnotherVersionOrSite_ReturnsNothing()
    {
        var site = Saved(await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("One")]));

        Assert.Empty(await _bed.SectionsOfAsync(new VersionedKey<Guid>(site.Key, 2)));
        Assert.Empty(await _bed.SectionsOfAsync(new VersionedKey<Guid>(Guid.NewGuid(), 1)));
    }

    [Fact]
    public async Task Delete_KeepsTheSectionsForARestore()
    {
        var site = Saved(await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("One")]));

        Assert.True(await _bed.WithSiteCommandsAsync(commands => commands.Delete(site.Key)));
        Assert.IsType<SiteSaveResult.Saved>(await _bed.WithSiteCommandsAsync(commands => commands.Restore(site.Key)));

        Assert.Equal(["One"], (await _bed.SectionsOfAsync(site)).Select(section => section.Title));
    }

    [Fact]
    public async Task HardDelete_RemovesTheSectionsOfTheSite()
    {
        var site = Saved(await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("One")]));
        var other = Saved(await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("Kept")], "other"));
        await _bed.WithSiteCommandsAsync(commands => commands.Delete(site.Key));

        Assert.Equal(HardDeleteResult.Deleted, await _bed.WithSiteCommandsAsync(commands => commands.HardDelete(site.Key)));

        Assert.Empty(await _bed.SectionsOfAsync(site));
        Assert.Equal(["Kept"], (await _bed.SectionsOfAsync(other)).Select(section => section.Title));
        await using var ctx = _bed.Read();
        Assert.DoesNotContain(
            await ctx.SiteSections.Select(section => EF.Property<Guid>(section, SiteSectionConfiguration.SiteIdProperty)).ToListAsync(),
            siteId => siteId == site.Key);
    }

    [Fact]
    public async Task HardDelete_OfALiveSite_IsRejectedAndKeepsTheSections()
    {
        var site = Saved(await _bed.CreateSiteWithSectionsAsync([SiteSectionTestBedExtensions.SectionDraft("One")]));

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithSiteCommandsAsync(commands => commands.HardDelete(site.Key)));

        Assert.Single(await _bed.SectionsOfAsync(site));
    }
}
