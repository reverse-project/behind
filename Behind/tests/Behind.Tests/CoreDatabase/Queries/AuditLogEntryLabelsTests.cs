using Microsoft.EntityFrameworkCore;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class AuditLogEntryLabelsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ResolveLabels_NoIdentifiers_ReturnsNoLabels()
    {
        await using var ctx = _factory.CreateContext();

        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([]);

        Assert.Empty(labels.Values);
    }

    [Fact]
    public async Task ResolveLabels_NamesUsersEmailsRolesAndGrants()
    {
        Guid userId;
        Guid emailId;
        Guid roleId;
        Guid grantId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "ada@test.com");
            emailId = await DatabaseSeedHelper.SeedEmailAsync(seed, userId, "ada2@test.com");
            roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "Editors");
            grantId = await DatabaseSeedHelper.SeedGrantAsync(seed, "Posts.Write");
        }

        await using var ctx = _factory.CreateContext();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([userId, emailId, roleId, grantId]);

        Assert.Equal("Test User", labels.Label(userId.ToString()));
        Assert.Equal("ada2@test.com", labels.Label(emailId.ToString()));
        Assert.Equal("Editors", labels.Label(roleId.ToString()));
        Assert.Equal("Posts.Write", labels.Label(grantId.ToString()));
        Assert.Equal("Editors: Posts.Write", labels.LabelKey($"{roleId}:{grantId}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesBuiltInUsers()
    {
        await using var ctx = _factory.CreateContext();

        var labels = await new AuditLogEntryQueries(ctx, [])
            .ResolveLabels([DomainConstants.SystemUserId, DomainConstants.GuestUserId]);

        Assert.Equal("System", labels.Label(DomainConstants.SystemUserId.ToString()));
        Assert.Equal("Guest", labels.Label(DomainConstants.GuestUserId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesOwnedEntitiesAfterTheirUser()
    {
        Guid userId;
        Guid shameId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "owner@test.com");
            shameId = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(seed, userId, userId);
        }

        await using var ctx = _factory.CreateContext();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([shameId]);

        Assert.Equal("Wall of shame entry of Test User", labels.Label(shameId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesTwoFactorEntitiesAfterTheirUser()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "owner@test.com");
            var commands = new TwoFactorCommands(seed);
            await commands.SaveTotp(userId, "SECRET");
            await commands.AddPasskey(userId, new UserPasskey("Laptop", "credential", "key"));
            await commands.ReplaceRecoveryCodes(userId, ["hash"]);
        }

        await using var ctx = _factory.CreateContext();
        var totpId = await ctx.UserTotps.Select(totp => EF.Property<Guid>(totp, "Id")).SingleAsync();
        var passkeyId = await ctx.UserPasskeys.Select(passkey => EF.Property<Guid>(passkey, "Id")).SingleAsync();
        var codeId = await ctx.UserRecoveryCodes.Select(code => EF.Property<Guid>(code, "Id")).SingleAsync();

        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([totpId, passkeyId, codeId]);

        Assert.Equal("Authenticator app of Test User", labels.Label(totpId.ToString()));
        Assert.Equal("Passkey of Test User", labels.Label(passkeyId.ToString()));
        Assert.Equal("Recovery code of Test User", labels.Label(codeId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesSitesAfterTheirLatestVersionTitle()
    {
        Guid siteId;
        await using (var seed = _factory.CreateContext())
        {
            await using var seedVersions = _factory.CreateVersioningContext();
            var commands = new SiteCommands(seed, seedVersions, TimeProvider.System, Substitute.For<IVfsCommands>(), Substitute.For<ICarouselCommands>());
            siteId = ((SiteSaveResult.Saved)await commands.Create("Old title", "old", "<p/>", true)).Id.Key;
        }

        await using (var update = _factory.CreateContext())
        {
            await using var updateVersions = _factory.CreateVersioningContext();
            await new SiteCommands(update, updateVersions, TimeProvider.System, Substitute.For<IVfsCommands>(), Substitute.For<ICarouselCommands>()).SaveVersion(siteId, "New title", "new", "<p/>", false);
        }

        await using var ctx = _factory.CreateContext();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([siteId]);

        Assert.Equal("New title", labels.Label(siteId.ToString()));
        Assert.Equal("New title: 2", labels.LabelKey($"{siteId}:2"));
        Assert.Equal("Site: New title", labels.LabelKey($"Site:{siteId}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesSiteImagesAfterTheirFileName()
    {
        using var bed = new NewsTestBed();
        var imageId = await bed.AddImageAsync("logo.png");

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([imageId]);

        Assert.Equal("logo.png", labels.Label(imageId.ToString()));
        Assert.Equal("VfsFile: logo.png", labels.LabelKey($"VfsFile:{imageId}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesNewsAfterTheirNewestTitleAndCategoriesAfterTheirName()
    {
        using var bed = new NewsTestBed();
        var categoryId = await bed.CreateCategoryAsync("Releases");
        var first = await bed.SaveNewsAsync(null, "Version 2", categoryId);
        await bed.SaveNewsAsync(first.Key, "Version 2 final", categoryId);

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([first.Key, categoryId]);

        Assert.Equal("Version 2 final", labels.Label(first.Key.ToString()));
        Assert.Equal("Releases", labels.Label(categoryId.ToString()));
        Assert.Equal("Version 2 final: 1", labels.LabelKey($"{first.Key}:1"));
        Assert.Equal("News: Version 2 final", labels.LabelKey($"News:{first.Key}"));
        Assert.Equal("Releases: 1", labels.LabelKey($"{categoryId}:1"));
    }

    [Fact]
    public async Task ResolveLabels_NamesNewsCommentsAfterTheirAuthor()
    {
        using var bed = new NewsTestBed();
        var news = await bed.CreateNewsAsync();
        var commentId = await bed.AddCommentAsync(news, "Hi", bed.OtherAuthorId);

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([commentId]);

        Assert.Equal("News comment of Test User", labels.Label(commentId.ToString()));
        Assert.Equal("News comment of Test User: 1", labels.LabelKey($"{commentId}:1"));
    }

    [Fact]
    public async Task ResolveLabels_NamesDeletedNewsBecauseTheAuditLogIncludesDeletedRecords()
    {
        using var bed = new NewsTestBed();
        var newsId = await bed.CreateNewsAsync("Gone");
        await bed.WithNewsCommandsAsync(commands => commands.Delete(newsId));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([newsId]);

        Assert.Equal("Gone", labels.Label(newsId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesArticlesAfterTheirNewestTitleAndArticleCategoriesAfterTheirName()
    {
        using var bed = new NewsTestBed();
        var categoryId = await bed.CreateArticleCategoryAsync("Tutorials");
        var first = await bed.SaveArticleAsync(null, "Setup", categoryId);
        await bed.SaveArticleAsync(first.Key, "Setup final", categoryId);

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([first.Key, categoryId]);

        Assert.Equal("Setup final", labels.Label(first.Key.ToString()));
        Assert.Equal("Tutorials", labels.Label(categoryId.ToString()));
        Assert.Equal("Setup final: 1", labels.LabelKey($"{first.Key}:1"));
        Assert.Equal("Article: Setup final", labels.LabelKey($"Article:{first.Key}"));
        Assert.Equal("Tutorials: 1", labels.LabelKey($"{categoryId}:1"));
    }

    [Fact]
    public async Task ResolveLabels_NamesDeletedArticlesBecauseTheAuditLogIncludesDeletedRecords()
    {
        using var bed = new NewsTestBed();
        var articleId = await bed.CreateArticleAsync("Gone");
        await bed.WithArticleCommandsAsync(commands => commands.Delete(articleId));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([articleId]);

        Assert.Equal("Gone", labels.Label(articleId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesGalleriesAfterTheirNewestTitleAndGalleryItemsAfterTheirDescription()
    {
        using var bed = new NewsTestBed();
        var items = await bed.NewGalleryItemsAsync("Sunset on the beach");
        var first = await bed.SaveGalleryAsync(null, "Trip", items: items);
        await bed.SaveGalleryAsync(first.Key, "Trip final", items: items);

        await using var ctx = bed.Read();
        var itemKey = await ctx.GalleryItems.Select(item => EF.Property<Guid>(item, VersionedColumns.Key)).FirstAsync();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([first.Key, itemKey]);

        Assert.Equal("Trip final", labels.Label(first.Key.ToString()));
        Assert.Equal("Sunset on the beach", labels.Label(itemKey.ToString()));
        Assert.Equal("Trip final: 1", labels.LabelKey($"{first.Key}:1"));
        Assert.Equal("Gallery: Trip final", labels.LabelKey($"Gallery:{first.Key}"));
        Assert.Equal("GalleryItem: Sunset on the beach", labels.LabelKey($"GalleryItem:{itemKey}"));
    }

    [Fact]
    public async Task ResolveLabels_ShortensLongGalleryItemDescriptions()
    {
        using var bed = new NewsTestBed();
        var items = await bed.NewGalleryItemsAsync(new string('x', 500));
        await bed.CreateGalleryAsync(items: items);

        await using var ctx = bed.Read();
        var itemKey = await ctx.GalleryItems.Select(item => EF.Property<Guid>(item, VersionedColumns.Key)).FirstAsync();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([itemKey]);

        Assert.Equal(80, labels.Label(itemKey.ToString()).Length);
        Assert.EndsWith("…", labels.Label(itemKey.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesGalleryImagesAfterTheirFileName()
    {
        using var bed = new NewsTestBed();
        var imageId = await bed.AddGalleryImageAsync("sunset.png");

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([imageId]);

        Assert.Equal("sunset.png", labels.Label(imageId.ToString()));
        Assert.Equal("VfsFile: sunset.png", labels.LabelKey($"VfsFile:{imageId}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesGalleryItemsWithoutADescriptionImage()
    {
        using var bed = new NewsTestBed();
        await bed.CreateGalleryAsync(items: await bed.NewGalleryItemsAsync(" "));

        await using var ctx = bed.Read();
        var itemKey = await ctx.GalleryItems.Select(item => EF.Property<Guid>(item, VersionedColumns.Key)).FirstAsync();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([itemKey]);

        Assert.Equal("Image", labels.Label(itemKey.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesDeletedGalleriesBecauseTheAuditLogIncludesDeletedRecords()
    {
        using var bed = new NewsTestBed();
        var galleryId = await bed.CreateGalleryAsync("Gone");
        await bed.WithGalleryCommandsAsync(commands => commands.Delete(galleryId));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([galleryId]);

        Assert.Equal("Gone", labels.Label(galleryId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesCarouselsAfterTheirSiteAndSlidesAfterTheirCaption()
    {
        using var bed = new NewsTestBed();
        var siteId = await bed.CreateSiteAsync("Landing", "landing");
        await bed.SaveCarouselAsync(siteId, await bed.NewCarouselDraftAsync(5, "Sunrise", " "));

        await using var ctx = bed.Read();
        var slideKeys = await ctx.CarouselSlides
            .OrderBy(slide => EF.Property<int>(slide, "Position"))
            .Select(slide => EF.Property<Guid>(slide, VersionedColumns.Key))
            .ToListAsync();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([siteId, .. slideKeys]);

        Assert.Equal("Landing", labels.Label(siteId.ToString()));
        Assert.Equal("Carousel: Landing", labels.LabelKey($"Carousel:{siteId}"));
        Assert.Equal("Sunrise", labels.Label(slideKeys[0].ToString()));
        Assert.Equal("Image", labels.Label(slideKeys[1].ToString()));
        Assert.Equal($"CarouselSlide: Sunrise", labels.LabelKey($"CarouselSlide:{slideKeys[0]}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesSiteSectionsAfterTheirTitleOrSection()
    {
        using var bed = new NewsTestBed();
        var site = ((SiteSaveResult.Saved)await bed.CreateSiteWithSectionsAsync(
        [
            SiteSectionTestBedExtensions.SectionDraft("Our story"),
            SiteSectionTestBedExtensions.SectionDraft(string.Empty, "<p>x</p>")
        ])).Id;
        var sections = await bed.SectionsOfAsync(site);

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels(sections.Select(section => section.Id).ToList());

        Assert.Equal("Our story", labels.Label(sections[0].Id.ToString()));
        Assert.Equal("Section", labels.Label(sections[1].Id.ToString()));
        Assert.Equal($"SiteSection: Our story", labels.LabelKey($"SiteSection:{sections[0].Id}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesDownloadFoldersAndDownloadsAfterTheirNewestDisplayName()
    {
        using var bed = new NewsTestBed();
        var folder = await bed.CreateDownloadFolderAsync("Manuals", "manuals");
        await bed.WithDownloadFolderCommandsAsync(
            commands => commands.Update(folder, new IDownloadFolderCommands.FolderDraft("guides", "Guides")));
        var download = await bed.UploadDownloadAsync(folder, "User guide");
        await bed.WithDownloadCommandsAsync(commands => commands.Rename(download, "Handbook"));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([folder, download]);

        Assert.Equal("Guides", labels.Label(folder.ToString()));
        Assert.Equal("Handbook", labels.Label(download.ToString()));
        Assert.Equal("DownloadFolder: Guides", labels.LabelKey($"DownloadFolder:{folder}"));
        Assert.Equal("Download: Handbook", labels.LabelKey($"Download:{download}"));
        Assert.Equal("Handbook: 2", labels.LabelKey($"{download}:2"));
    }

    [Fact]
    public async Task ResolveLabels_NamesDeletedDownloadsBecauseTheAuditLogIncludesDeletedRecords()
    {
        using var bed = new NewsTestBed();
        var folder = await bed.CreateDownloadFolderAsync("Gone folder", "gone");
        var download = await bed.UploadDownloadAsync(folder, "Gone file");
        await bed.WithDownloadCommandsAsync(commands => commands.Delete(download));
        await bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(folder));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([folder, download]);

        Assert.Equal("Gone folder", labels.Label(folder.ToString()));
        Assert.Equal("Gone file", labels.Label(download.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesMenusAfterTheirNewestTitleAndMenuItemsAfterTheirTitle()
    {
        using var bed = new NewsTestBed();
        var menuId = await bed.CreateMenuAsync("footer", "Footer");
        await bed.WithMenuCommandsAsync(commands => commands.Update(
            menuId,
            new IMenuCommands.MenuDraft("footer", "Page footer")));
        var first = await bed.SaveMenuItemAsync(null, menuId, MenuTestBedExtensions.ItemDraft("Docs"));
        await bed.SaveMenuItemAsync(first.Key, menuId, MenuTestBedExtensions.ItemDraft("Documentation"));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([menuId, first.Key]);

        Assert.Equal("Page footer", labels.Label(menuId.ToString()));
        Assert.Equal("Documentation", labels.Label(first.Key.ToString()));
        Assert.Equal("Page footer: 1", labels.LabelKey($"{menuId}:1"));
        Assert.Equal("MenuItem: Documentation", labels.LabelKey($"MenuItem:{first.Key}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesDeletedMenusAndMenuItemsBecauseTheAuditLogIncludesDeletedRecords()
    {
        using var bed = new NewsTestBed();
        var menuId = await bed.CreateMenuAsync("footer", "Footer");
        var itemId = await bed.CreateMenuItemAsync(menuId, "Docs");
        await bed.WithMenuItemCommandsAsync(commands => commands.Delete(itemId));
        await bed.WithMenuCommandsAsync(commands => commands.Delete(menuId));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([menuId, itemId]);

        Assert.Equal("Footer", labels.Label(menuId.ToString()));
        Assert.Equal("Docs", labels.Label(itemId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesRouteAliasesAfterTheirNewestAliasEvenWhenDeleted()
    {
        using var bed = new NewsTestBed();
        var aliasId = await bed.CreateRouteAliasAsync("manuals", "#/downloads/a");
        await bed.WithRouteAliasCommandsAsync(commands => commands.Update(
            aliasId,
            RouteAliasTestBedExtensions.Draft("handbooks", "#/downloads/b")));
        var goneId = await bed.CreateRouteAliasAsync("gone", "#/news");
        await bed.WithRouteAliasCommandsAsync(commands => commands.Delete(goneId));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([aliasId, goneId]);

        Assert.Equal("handbooks", labels.Label(aliasId.ToString()));
        Assert.Equal("gone", labels.Label(goneId.ToString()));
        Assert.Equal("handbooks: 1", labels.LabelKey($"{aliasId}:1"));
    }

    [Fact]
    public async Task ResolveLabels_NamesTheAccessSettingsByTheirKind()
    {
        using var bed = new NewsTestBed();
        var saved = await bed.SaveAccessSettingsAsync();
        await bed.SaveAccessSettingsAsync(AccessSettingsTestBedExtensions.AccessDraft(message: "Again"));

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([saved.Id.Key]);

        Assert.Equal("Access settings", labels.Label(saved.Id.Key.ToString()));
        Assert.Equal("Access settings: 2", labels.LabelKey($"{saved.Id.Key}:2"));
    }

    [Fact]
    public async Task ResolveLabels_UnknownIdentifierIsKeptAsIs()
    {
        var unknown = Guid.NewGuid();
        await using var ctx = _factory.CreateContext();

        var labels = await new AuditLogEntryQueries(ctx, []).ResolveLabels([unknown]);

        Assert.Equal(unknown.ToString(), labels.Label(unknown.ToString()));
        Assert.Equal("plain", labels.Label("plain"));
        Assert.Null(labels.LabelOrNull(null));
    }
}
