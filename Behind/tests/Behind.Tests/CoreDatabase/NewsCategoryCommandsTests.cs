using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class NewsCategoryCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static INewsCategoryCommands.NewsCategoryDraft Draft(string name = "Releases", Option<Guid> image = default) =>
        new(name, image);

    private async Task<T> CommandsAsync<T>(Func<NewsCategoryCommands, Task<T>> action)
    {
        await using var ctx = _bed.Write();

        return await action(new NewsCategoryCommands(ctx, Substitute.For<IVfsCommands>()));
    }

    private async Task<IReadOnlyList<(long Version, string Name, Guid? Image)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        var rows = await ctx.NewsCategories
            .OfKey(key)
            .OrderBy(category => EF.Property<long>(category, VersionedColumns.Version))
            .Select(category => new
            {
                Version = EF.Property<long>(category, VersionedColumns.Version),
                category.Name,
                Image = EF.Property<Guid?>(category, NewsCategoryConfiguration.HeaderImageIdProperty)
            })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Name, row.Image)).ToList();
    }

    [Fact]
    public async Task Create_StoresVersionOneWithTheNameAndTheHeaderImage()
    {
        var imageId = await _bed.AddImageAsync();

        var key = await _bed.CreateCategoryAsync("Releases", imageId);

        Assert.Equal([(1L, "Releases", (Guid?)imageId)], await VersionsAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("NewsCategory", key));
    }

    [Fact]
    public async Task Create_WithoutImage_StoresNoImage()
    {
        var key = await _bed.CreateCategoryAsync("Plain");

        Assert.Null(Assert.Single(await VersionsAsync(key)).Image);
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_IsRejected()
    {
        await _bed.CreateCategoryAsync("Releases");

        Assert.IsType<NewsCategorySaveResult.NameTaken>(await CommandsAsync(commands => commands.Create(Draft("RELEASES"))));
    }

    [Fact]
    public async Task Create_UnknownImage_IsRejected()
    {
        Assert.IsType<NewsCategorySaveResult.UnknownImage>(
            await CommandsAsync(commands => commands.Create(Draft(image: Guid.NewGuid()))));
    }

    [Fact]
    public async Task Create_GalleryImageAsHeaderImage_IsRejected()
    {
        var galleryImage = await _bed.AddGalleryImageAsync();

        Assert.IsType<NewsCategorySaveResult.UnknownImage>(
            await CommandsAsync(commands => commands.Create(Draft(image: galleryImage))));
    }

    [Fact]
    public async Task Create_NameOfADeletedCategory_IsAllowed()
    {
        var key = await _bed.CreateCategoryAsync("Releases");
        await CommandsAsync(commands => commands.Delete(key));

        Assert.IsType<NewsCategorySaveResult.Saved>(await CommandsAsync(commands => commands.Create(Draft("Releases"))));
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionAndKeepsTheOldOne()
    {
        var key = await _bed.CreateCategoryAsync("Releases");
        var imageId = await _bed.AddImageAsync();

        var result = await CommandsAsync(commands => commands.Update(key, Draft("Announcements", imageId)));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<NewsCategorySaveResult.Saved>(result).Id);
        Assert.Equal([(1L, "Releases", (Guid?)null), (2L, "Announcements", imageId)], await VersionsAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("NewsCategory", key, 2));
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_IsAllowed()
    {
        var key = await _bed.CreateCategoryAsync("Releases");

        Assert.IsType<NewsCategorySaveResult.Saved>(await CommandsAsync(commands => commands.Update(key, Draft("releases"))));
    }

    [Fact]
    public async Task Update_NameOfAnotherCategory_IsRejected()
    {
        await _bed.CreateCategoryAsync("Releases");
        var key = await _bed.CreateCategoryAsync("Events");

        Assert.IsType<NewsCategorySaveResult.NameTaken>(await CommandsAsync(commands => commands.Update(key, Draft("Releases"))));
        Assert.Single(await VersionsAsync(key));
    }

    [Fact]
    public async Task Update_UnknownImage_IsRejected()
    {
        var key = await _bed.CreateCategoryAsync();

        Assert.IsType<NewsCategorySaveResult.UnknownImage>(
            await CommandsAsync(commands => commands.Update(key, Draft(image: Guid.NewGuid()))));
    }

    [Fact]
    public async Task Update_UnknownOrDeletedCategory_ReturnsNotFound()
    {
        var key = await _bed.CreateCategoryAsync();
        await CommandsAsync(commands => commands.Delete(key));

        Assert.IsType<NewsCategorySaveResult.NotFound>(await CommandsAsync(commands => commands.Update(key, Draft())));
        Assert.IsType<NewsCategorySaveResult.NotFound>(await CommandsAsync(commands => commands.Update(Guid.NewGuid(), Draft())));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersionAndKeepsTheNews()
    {
        var key = await _bed.CreateCategoryAsync();
        await CommandsAsync(commands => commands.Update(key, Draft("Renamed")));
        var news = await _bed.CreateNewsAsync(categoryId: key);

        Assert.True(await CommandsAsync(commands => commands.Delete(key)));

        await using var read = _bed.Read();
        Assert.Empty(await read.NewsCategories.ToListAsync());
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("NewsCategory", key, 1));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("NewsCategory", key, 2));
        Assert.Single(await read.NewsEntries.ToListAsync());
        Assert.Equal(["Created"], await _bed.ActionsAsync("News", news));
    }

    [Fact]
    public async Task Delete_UnknownCategory_ReturnsFalse()
    {
        Assert.False(await CommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsEveryVersionBack()
    {
        var key = await _bed.CreateCategoryAsync();
        await CommandsAsync(commands => commands.Update(key, Draft("Renamed")));
        await CommandsAsync(commands => commands.Delete(key));

        var result = await CommandsAsync(commands => commands.Restore(key));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<NewsCategorySaveResult.Saved>(result).Id);
        await using var read = _bed.Read();
        Assert.Equal(2, (await read.NewsCategories.ToListAsync()).Count);
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("NewsCategory", key, 1));
    }

    [Fact]
    public async Task Restore_WhenTheNameWasTakenMeanwhile_IsRejected()
    {
        var key = await _bed.CreateCategoryAsync("Releases");
        await CommandsAsync(commands => commands.Delete(key));
        await _bed.CreateCategoryAsync("Releases");

        Assert.IsType<NewsCategorySaveResult.NameTaken>(await CommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task Restore_ActiveOrUnknownCategory_ReturnsNotFound()
    {
        var key = await _bed.CreateCategoryAsync();

        Assert.IsType<NewsCategorySaveResult.NotFound>(await CommandsAsync(commands => commands.Restore(key)));
        Assert.IsType<NewsCategorySaveResult.NotFound>(await CommandsAsync(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task HardDelete_RemovesDeletedCategoryVersionsAndKeepsTheNews()
    {
        var key = await _bed.CreateCategoryAsync();
        await CommandsAsync(commands => commands.Update(key, Draft("Renamed")));
        await _bed.CreateNewsAsync(categoryId: key);
        await CommandsAsync(commands => commands.Delete(key));

        Assert.Equal(HardDeleteResult.Deleted, await CommandsAsync(commands => commands.HardDelete(key)));

        await using var read = _bed.Read();
        using var scope = read.SoftDelete.IncludeDeleted();
        Assert.Empty(await read.NewsCategories.ToListAsync());
        Assert.Single(await read.NewsEntries.ToListAsync());
    }

    [Fact]
    public async Task HardDelete_ActiveOrUnknownCategory_IsNotFound()
    {
        var key = await _bed.CreateCategoryAsync();

        Assert.Equal(HardDeleteResult.NotFound, await CommandsAsync(commands => commands.HardDelete(key)));
        Assert.Equal(HardDeleteResult.NotFound, await CommandsAsync(commands => commands.HardDelete(Guid.NewGuid())));
    }
}
