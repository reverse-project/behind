using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class ArticleCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private sealed record Stored(long Version, string Title, string Summary, string Content, Guid Author, Guid? Category);

    private async Task<IReadOnlyList<Stored>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();

        return await ctx.Articles
            .OfKey(key)
            .OrderBy(article => EF.Property<long>(article, VersionedColumns.Version))
            .Select(article => new Stored(
                EF.Property<long>(article, VersionedColumns.Version),
                article.Title,
                article.Summary,
                article.Content,
                EF.Property<Guid>(article, ArticleConfiguration.AuthorIdProperty),
                EF.Property<Guid?>(article, ArticleConfiguration.CategoryIdProperty)))
            .ToListAsync();
    }

    private async Task<long?> ActiveVersionAsync(Guid key)
    {
        await using var versions = _bed.ReadVersions();
        var active = await versions.ActiveVersionsAsync(Article.EntityType, [key.ToString()]);

        return active.TryGetValue(key.ToString(), out var version) ? version : null;
    }

    private static IArticleCommands.ArticleDraft Draft(string title = "Guide", Option<Guid> category = default) =>
        new(title, "<p>short</p>", "<p>long</p>", category);

    [Fact]
    public async Task Create_Publish_StoresVersionOneAndMakesItLive()
    {
        var key = await _bed.CreateArticleAsync("Setup", authorId: _bed.OtherAuthorId);

        var stored = Assert.Single(await VersionsAsync(key));
        Assert.Equal(
            (1L, "Setup", "<p>short</p>", "<p>long</p>", _bed.OtherAuthorId, (Guid?)null),
            (stored.Version, stored.Title, stored.Summary, stored.Content, stored.Author, stored.Category));
        Assert.Equal(1L, await ActiveVersionAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("Article", key));
        Assert.Equal(new AuditTimes(Some(NewsTestBed.Now), None), await _bed.TimesAsync<Article>(key, 1));
    }

    [Fact]
    public async Task Create_Draft_StoresVersionOneWithoutMakingItLive()
    {
        var key = await _bed.CreateArticleAsync(publish: false);

        Assert.Single(await VersionsAsync(key));
        Assert.Null(await ActiveVersionAsync(key));
    }

    [Fact]
    public async Task Create_WithCategory_LinksTheCategoryKey()
    {
        var category = await _bed.CreateArticleCategoryAsync();

        var key = await _bed.CreateArticleAsync(categoryId: category);

        Assert.Equal(category, Assert.Single(await VersionsAsync(key)).Category);
    }

    [Fact]
    public async Task Create_WithUnknownOrDeletedCategory_IsRejectedAndStoresNothing()
    {
        var deleted = await _bed.CreateArticleCategoryAsync("Gone");
        await using (var ctx = _bed.Write())
        {
            await new ArticleCategoryCommands(ctx, Substitute.For<IVfsCommands>()).Delete(deleted);
        }

        foreach (var category in new[] { Guid.NewGuid(), deleted })
        {
            var result = await _bed.WithArticleCommandsAsync(commands =>
                commands.Create(_bed.AuthorId, Draft(category: category), true));

            Assert.IsType<ArticleSaveResult.UnknownCategory>(result);
        }

        await using var read = _bed.Read();
        Assert.Empty(await read.Articles.ToListAsync());
    }

    [Fact]
    public async Task Create_NewsCategoriesAreNotArticleCategories()
    {
        var newsCategory = await _bed.CreateCategoryAsync();

        var result = await _bed.WithArticleCommandsAsync(commands =>
            commands.Create(_bed.AuthorId, Draft(category: newsCategory), true));

        Assert.IsType<ArticleSaveResult.UnknownCategory>(result);
    }

    [Fact]
    public async Task SaveVersion_InsertsTheNextVersionAndNeverTouchesTheOldOne()
    {
        var first = await _bed.SaveArticleAsync(null, "First", authorId: _bed.OtherAuthorId);

        var second = await _bed.SaveArticleAsync(first.Key, "Second", at: NewsTestBed.Now.AddHours(1));

        Assert.Equal(new VersionedKey<Guid>(first.Key, 2), second);
        var versions = await VersionsAsync(first.Key);
        Assert.Equal(["First", "Second"], versions.Select(version => version.Title));
        Assert.Equal([_bed.OtherAuthorId, _bed.OtherAuthorId], versions.Select(version => version.Author));
        Assert.Equal(["Created"], await _bed.ActionsAsync("Article", first.Key, 1));
        Assert.Equal(["Created"], await _bed.ActionsAsync("Article", first.Key, 2));
    }

    [Fact]
    public async Task SaveVersion_Publish_MakesTheNewVersionLive()
    {
        var first = await _bed.SaveArticleAsync(null);

        await _bed.SaveArticleAsync(first.Key, publish: true);

        Assert.Equal(2L, await ActiveVersionAsync(first.Key));
    }

    [Fact]
    public async Task SaveVersion_Draft_KeepsTheLiveVersion()
    {
        var first = await _bed.SaveArticleAsync(null);

        await _bed.SaveArticleAsync(first.Key, publish: false);

        Assert.Equal(1L, await ActiveVersionAsync(first.Key));
        Assert.Equal(2, (await VersionsAsync(first.Key)).Count);
    }

    [Fact]
    public async Task SaveVersion_TheUpdatedTimeIsTheCreationOfTheShownVersion()
    {
        var first = await _bed.SaveArticleAsync(null, at: NewsTestBed.Now);
        await _bed.SaveArticleAsync(first.Key, at: NewsTestBed.Now.AddHours(3));

        Assert.Equal(
            new AuditTimes(Some(NewsTestBed.Now), Some(NewsTestBed.Now.AddHours(3))),
            await _bed.TimesAsync<Article>(first.Key, 2));
    }

    [Fact]
    public async Task SaveVersion_UnknownOrDeletedArticle_ReturnsNotFound()
    {
        var key = await _bed.CreateArticleAsync();
        await _bed.WithArticleCommandsAsync(commands => commands.Delete(key));

        Assert.IsType<ArticleSaveResult.NotFound>(
            await _bed.WithArticleCommandsAsync(commands => commands.SaveVersion(key, Draft(), true)));
        Assert.IsType<ArticleSaveResult.NotFound>(
            await _bed.WithArticleCommandsAsync(commands => commands.SaveVersion(Guid.NewGuid(), Draft(), true)));
    }

    [Fact]
    public async Task SaveVersion_UnknownCategory_InsertsNothing()
    {
        var key = await _bed.CreateArticleAsync();

        var result = await _bed.WithArticleCommandsAsync(commands =>
            commands.SaveVersion(key, Draft(category: Guid.NewGuid()), true));

        Assert.IsType<ArticleSaveResult.UnknownCategory>(result);
        Assert.Single(await VersionsAsync(key));
    }

    [Fact]
    public async Task Activate_SwitchesTheLiveVersionIncludingBackToAnOlderOne()
    {
        var first = await _bed.SaveArticleAsync(null, "First");
        await _bed.SaveArticleAsync(first.Key, "Second");

        var result = await _bed.WithArticleCommandsAsync(commands => commands.Activate(first));

        Assert.Equal(first, Assert.IsType<ArticleSaveResult.Saved>(result).Id);
        Assert.Equal(1L, await ActiveVersionAsync(first.Key));
    }

    [Fact]
    public async Task Activate_MakesADraftLive()
    {
        var draft = await _bed.SaveArticleAsync(null, publish: false);

        await _bed.WithArticleCommandsAsync(commands => commands.Activate(draft));

        Assert.Equal(1L, await ActiveVersionAsync(draft.Key));
    }

    [Fact]
    public async Task Activate_UnknownVersion_ReturnsNotFound()
    {
        var key = await _bed.CreateArticleAsync();

        Assert.IsType<ArticleSaveResult.NotFound>(
            await _bed.WithArticleCommandsAsync(commands => commands.Activate(new VersionedKey<Guid>(key, 9))));
        Assert.IsType<ArticleSaveResult.NotFound>(
            await _bed.WithArticleCommandsAsync(commands =>
                commands.Activate(new VersionedKey<Guid>(Guid.NewGuid(), 1))));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersion()
    {
        var first = await _bed.SaveArticleAsync(null);
        await _bed.SaveArticleAsync(first.Key, publish: false);

        Assert.True(await _bed.WithArticleCommandsAsync(commands => commands.Delete(first.Key)));

        await using var read = _bed.Read();
        Assert.Empty(await read.Articles.ToListAsync());
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Article", first.Key, 1));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Article", first.Key, 2));
    }

    [Fact]
    public async Task Delete_UnknownOrAlreadyDeletedArticle_ReturnsFalse()
    {
        var key = await _bed.CreateArticleAsync();
        await _bed.WithArticleCommandsAsync(commands => commands.Delete(key));

        Assert.False(await _bed.WithArticleCommandsAsync(commands => commands.Delete(key)));
        Assert.False(await _bed.WithArticleCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsBackEveryVersionAndTheLiveVersion()
    {
        var first = await _bed.SaveArticleAsync(null);
        await _bed.SaveArticleAsync(first.Key);
        await _bed.WithArticleCommandsAsync(commands => commands.Delete(first.Key));

        Assert.True(await _bed.WithArticleCommandsAsync(commands => commands.Restore(first.Key)));

        await using var read = _bed.Read();
        Assert.Equal(2, (await read.Articles.ToListAsync()).Count);
        Assert.Equal(2L, await ActiveVersionAsync(first.Key));
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("Article", first.Key, 2));
    }

    [Fact]
    public async Task Restore_ActiveOrUnknownArticle_ReturnsFalse()
    {
        var key = await _bed.CreateArticleAsync();

        Assert.False(await _bed.WithArticleCommandsAsync(commands => commands.Restore(key)));
        Assert.False(await _bed.WithArticleCommandsAsync(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task HardDelete_RemovesDeletedArticleWithItsVersionsAndLiveVersion()
    {
        var first = await _bed.SaveArticleAsync(null);
        await _bed.SaveArticleAsync(first.Key);
        await _bed.WithArticleCommandsAsync(commands => commands.Delete(first.Key));

        var result = await _bed.WithArticleCommandsAsync(commands => commands.HardDelete(first.Key));

        Assert.Equal(HardDeleteResult.Deleted, result);
        Assert.Empty(await VersionsAsync(first.Key));
        Assert.Null(await ActiveVersionAsync(first.Key));
        Assert.Contains("HardDeleted", await _bed.ActionsAsync("Article", first.Key, 1));
    }

    [Fact]
    public async Task HardDelete_ActiveOrUnknownArticle_IsNotFound()
    {
        var key = await _bed.CreateArticleAsync();

        Assert.Equal(
            HardDeleteResult.NotFound,
            await _bed.WithArticleCommandsAsync(commands => commands.HardDelete(key)));
        Assert.Equal(
            HardDeleteResult.NotFound,
            await _bed.WithArticleCommandsAsync(commands => commands.HardDelete(Guid.NewGuid())));
        Assert.Single(await VersionsAsync(key));
    }
}
