using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class ArticleCategoryCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static IArticleCategoryCommands.ArticleCategoryDraft Draft(
        string name = "Tutorials",
        Option<Guid> image = default) =>
        new(name, image);

    private async Task<T> CommandsAsync<T>(Func<ArticleCategoryCommands, Task<T>> action)
    {
        await using var ctx = _bed.Write();

        return await action(new ArticleCategoryCommands(ctx));
    }

    private async Task<IReadOnlyList<(long Version, string Name, Guid? Image)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        var rows = await ctx.ArticleCategories
            .OfKey(key)
            .OrderBy(category => EF.Property<long>(category, VersionedColumns.Version))
            .Select(category => new
            {
                Version = EF.Property<long>(category, VersionedColumns.Version),
                category.Name,
                Image = EF.Property<Guid?>(category, ArticleCategoryConfiguration.HeaderImageIdProperty)
            })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Name, row.Image)).ToList();
    }

    [Fact]
    public async Task Create_StoresVersionOneWithTheNameAndTheHeaderImage()
    {
        var imageId = await _bed.AddImageAsync();

        var key = await _bed.CreateArticleCategoryAsync("Tutorials", imageId);

        Assert.Equal([(1L, "Tutorials", (Guid?)imageId)], await VersionsAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("ArticleCategory", key));
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_IsRejected()
    {
        await _bed.CreateArticleCategoryAsync("Tutorials");

        Assert.IsType<ArticleCategorySaveResult.NameTaken>(
            await CommandsAsync(commands => commands.Create(Draft("TUTORIALS"))));
    }

    [Fact]
    public async Task Create_ANewsCategoryNameDoesNotClash()
    {
        await _bed.CreateCategoryAsync("Tutorials");

        Assert.IsType<ArticleCategorySaveResult.Saved>(
            await CommandsAsync(commands => commands.Create(Draft("Tutorials"))));
    }

    [Fact]
    public async Task Create_UnknownImage_IsRejected()
    {
        Assert.IsType<ArticleCategorySaveResult.UnknownImage>(
            await CommandsAsync(commands => commands.Create(Draft(image: Guid.NewGuid()))));
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionAndKeepsTheOldOne()
    {
        var key = await _bed.CreateArticleCategoryAsync("Tutorials");
        var imageId = await _bed.AddImageAsync();

        var result = await CommandsAsync(commands => commands.Update(key, Draft("Guides", imageId)));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<ArticleCategorySaveResult.Saved>(result).Id);
        Assert.Equal([(1L, "Tutorials", (Guid?)null), (2L, "Guides", imageId)], await VersionsAsync(key));
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_IsAllowed()
    {
        var key = await _bed.CreateArticleCategoryAsync("Tutorials");

        Assert.IsType<ArticleCategorySaveResult.Saved>(
            await CommandsAsync(commands => commands.Update(key, Draft("tutorials"))));
    }

    [Fact]
    public async Task Update_NameOfAnotherCategory_IsRejected()
    {
        await _bed.CreateArticleCategoryAsync("Tutorials");
        var key = await _bed.CreateArticleCategoryAsync("Events");

        Assert.IsType<ArticleCategorySaveResult.NameTaken>(
            await CommandsAsync(commands => commands.Update(key, Draft("Tutorials"))));
        Assert.Single(await VersionsAsync(key));
    }

    [Fact]
    public async Task Update_UnknownOrDeletedCategory_ReturnsNotFound()
    {
        var key = await _bed.CreateArticleCategoryAsync();
        await CommandsAsync(commands => commands.Delete(key));

        Assert.IsType<ArticleCategorySaveResult.NotFound>(
            await CommandsAsync(commands => commands.Update(key, Draft())));
        Assert.IsType<ArticleCategorySaveResult.NotFound>(
            await CommandsAsync(commands => commands.Update(Guid.NewGuid(), Draft())));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersionAndKeepsTheArticles()
    {
        var key = await _bed.CreateArticleCategoryAsync();
        await CommandsAsync(commands => commands.Update(key, Draft("Renamed")));
        var article = await _bed.CreateArticleAsync(categoryId: key);

        Assert.True(await CommandsAsync(commands => commands.Delete(key)));

        await using var read = _bed.Read();
        Assert.Empty(await read.ArticleCategories.ToListAsync());
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("ArticleCategory", key, 1));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("ArticleCategory", key, 2));
        Assert.Single(await read.Articles.ToListAsync());
        Assert.Equal(["Created"], await _bed.ActionsAsync("Article", article));
    }

    [Fact]
    public async Task Delete_UnknownCategory_ReturnsFalse()
    {
        Assert.False(await CommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsEveryVersionBack()
    {
        var key = await _bed.CreateArticleCategoryAsync();
        await CommandsAsync(commands => commands.Update(key, Draft("Renamed")));
        await CommandsAsync(commands => commands.Delete(key));

        var result = await CommandsAsync(commands => commands.Restore(key));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<ArticleCategorySaveResult.Saved>(result).Id);
        await using var read = _bed.Read();
        Assert.Equal(2, (await read.ArticleCategories.ToListAsync()).Count);
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("ArticleCategory", key, 1));
    }

    [Fact]
    public async Task Restore_WhenTheNameWasTakenMeanwhile_IsRejected()
    {
        var key = await _bed.CreateArticleCategoryAsync("Tutorials");
        await CommandsAsync(commands => commands.Delete(key));
        await _bed.CreateArticleCategoryAsync("Tutorials");

        Assert.IsType<ArticleCategorySaveResult.NameTaken>(await CommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task Restore_ActiveOrUnknownCategory_ReturnsNotFound()
    {
        var key = await _bed.CreateArticleCategoryAsync();

        Assert.IsType<ArticleCategorySaveResult.NotFound>(await CommandsAsync(commands => commands.Restore(key)));
        Assert.IsType<ArticleCategorySaveResult.NotFound>(
            await CommandsAsync(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task HardDelete_RemovesDeletedCategoryVersionsAndKeepsTheArticles()
    {
        var key = await _bed.CreateArticleCategoryAsync();
        await CommandsAsync(commands => commands.Update(key, Draft("Renamed")));
        await _bed.CreateArticleAsync(categoryId: key);
        await CommandsAsync(commands => commands.Delete(key));

        Assert.Equal(HardDeleteResult.Deleted, await CommandsAsync(commands => commands.HardDelete(key)));

        await using var read = _bed.Read();
        using var scope = read.SoftDelete.IncludeDeleted();
        Assert.Empty(await read.ArticleCategories.ToListAsync());
        Assert.Single(await read.Articles.ToListAsync());
    }

    [Fact]
    public async Task HardDelete_ActiveOrUnknownCategory_IsNotFound()
    {
        var key = await _bed.CreateArticleCategoryAsync();

        Assert.Equal(HardDeleteResult.NotFound, await CommandsAsync(commands => commands.HardDelete(key)));
        Assert.Equal(HardDeleteResult.NotFound, await CommandsAsync(commands => commands.HardDelete(Guid.NewGuid())));
    }
}
