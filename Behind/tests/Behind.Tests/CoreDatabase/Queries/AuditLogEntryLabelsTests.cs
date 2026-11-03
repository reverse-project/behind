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

        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([]);

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
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([userId, emailId, roleId, grantId]);

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

        var labels = await new AuditLogEntryQueries(ctx)
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
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([shameId]);

        Assert.Equal("Wall of shame entry of Test User", labels.Label(shameId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesSitesAfterTheirLatestVersionTitle()
    {
        Guid siteId;
        await using (var seed = _factory.CreateContext())
        {
            await using var seedVersions = _factory.CreateVersioningContext();
            var commands = new SiteCommands(seed, seedVersions, TimeProvider.System);
            siteId = ((SiteSaveResult.Saved)await commands.Create("Old title", "old", "<p/>", true)).Id.Key;
        }

        await using (var update = _factory.CreateContext())
        {
            await using var updateVersions = _factory.CreateVersioningContext();
            await new SiteCommands(update, updateVersions, TimeProvider.System).SaveVersion(siteId, "New title", "new", "<p/>", false);
        }

        await using var ctx = _factory.CreateContext();
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([siteId]);

        Assert.Equal("New title", labels.Label(siteId.ToString()));
        Assert.Equal("New title: 2", labels.LabelKey($"{siteId}:2"));
        Assert.Equal("Site: New title", labels.LabelKey($"Site:{siteId}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesSiteImagesAfterTheirFileName()
    {
        Guid imageId;
        await using (var seed = _factory.CreateContext())
        {
            imageId = await new SiteImageCommands(seed).Add(new SiteImage("logo.png", "image/png", [1], DateTimeOffset.UnixEpoch));
        }

        await using var ctx = _factory.CreateContext();
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([imageId]);

        Assert.Equal("logo.png", labels.Label(imageId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesNewsAfterTheirNewestTitleAndCategoriesAfterTheirName()
    {
        using var bed = new NewsTestBed();
        var categoryId = await bed.CreateCategoryAsync("Releases");
        var first = await bed.SaveNewsAsync(null, "Version 2", categoryId);
        await bed.SaveNewsAsync(first.Key, "Version 2 final", categoryId);

        await using var ctx = bed.Read();
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([first.Key, categoryId]);

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
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([commentId]);

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
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([newsId]);

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
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([first.Key, categoryId]);

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
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([articleId]);

        Assert.Equal("Gone", labels.Label(articleId.ToString()));
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
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([menuId, first.Key]);

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
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([menuId, itemId]);

        Assert.Equal("Footer", labels.Label(menuId.ToString()));
        Assert.Equal("Docs", labels.Label(itemId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_UnknownIdentifierIsKeptAsIs()
    {
        var unknown = Guid.NewGuid();
        await using var ctx = _factory.CreateContext();

        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([unknown]);

        Assert.Equal(unknown.ToString(), labels.Label(unknown.ToString()));
        Assert.Equal("plain", labels.Label("plain"));
        Assert.Null(labels.LabelOrNull(null));
    }
}
