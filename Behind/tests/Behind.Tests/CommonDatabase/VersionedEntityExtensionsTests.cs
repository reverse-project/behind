using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CommonDatabase;

public sealed class VersionedEntityExtensionsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static string Title(News news) => news.Title;

    [Fact]
    public async Task OfKey_OfKeys_AndOfVersion_SelectTheRowsOfTheRequestedEntities()
    {
        var first = await _bed.SaveNewsAsync(null, "First");
        await _bed.SaveNewsAsync(first.Key, "First 2");
        var second = await _bed.CreateNewsAsync("Second");
        await using var ctx = _bed.Read();

        Assert.Equal(["First", "First 2"], (await ctx.NewsEntries.OfKey(first.Key).OrderBy(news => news.Title).ToListAsync()).Select(Title));
        Assert.Equal(3, await ctx.NewsEntries.OfKeys([first.Key, second]).CountAsync());
        Assert.Equal(1, await ctx.NewsEntries.OfKeys([second]).CountAsync());
        Assert.Equal(0, await ctx.NewsEntries.OfKeys([]).CountAsync());
        Assert.Equal(
            "First 2",
            Title(await ctx.NewsEntries.OfVersion(new VersionedKey<Guid>(first.Key, 2)).SingleAsync()));
    }

    [Fact]
    public async Task LatestOf_KeepsOnlyTheHighestVersionOfEachKey()
    {
        var first = await _bed.SaveNewsAsync(null, "First");
        await _bed.SaveNewsAsync(first.Key, "First 2");
        await _bed.SaveNewsAsync(first.Key, "First 3");
        await _bed.CreateNewsAsync("Second");
        await using var ctx = _bed.Read();

        var latest = await ctx.NewsEntries.LatestOf(ctx.NewsEntries).OrderBy(news => news.Title).ToListAsync();

        Assert.Equal(["First 3", "Second"], latest.Select(Title));
    }

    [Fact]
    public async Task LatestVersionAsync_IsZeroWithoutRowsAndTheHighestVersionOtherwise()
    {
        var first = await _bed.SaveNewsAsync(null);
        await _bed.SaveNewsAsync(first.Key);
        await using var ctx = _bed.Read();

        Assert.Equal(2, await ctx.NewsEntries.LatestVersionAsync(first.Key));
        Assert.Equal(0, await ctx.NewsEntries.LatestVersionAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task VersionedId_AndStampVersionedKey_ReadAndWriteTheShadowKey()
    {
        var first = await _bed.SaveNewsAsync(null);
        await using var ctx = _bed.Read();
        var news = await ctx.NewsEntries.OfKey(first.Key).SingleAsync();

        Assert.Equal(first, ctx.Entry(news).VersionedId);

        var key = Guid.NewGuid();
        var added = new News("T", "", "", false);
        ctx.NewsEntries.Add(added);
        ctx.Entry(added).StampVersionedKey(key, 7);

        Assert.Equal(new VersionedKey<Guid>(key, 7), ctx.Entry(added).VersionedId);
    }

    [Fact]
    public async Task MarkSoftDeletedRestoredAndHardDeleted_ApplyToEveryVersionAndReportTheCount()
    {
        var first = await _bed.SaveNewsAsync(null);
        await _bed.SaveNewsAsync(first.Key);

        await using (var ctx = _bed.Write())
        {
            Assert.False(await ctx.HasDeletedRowsAsync(ctx.NewsEntries.OfKey(first.Key)));
            Assert.Equal(0, await ctx.MarkRestoredAsync(ctx.NewsEntries.OfKey(first.Key)));
            Assert.Equal(0, await ctx.MarkHardDeletedAsync(ctx.NewsEntries.OfKey(first.Key)));
            Assert.Equal(2, await ctx.MarkSoftDeletedAsync(ctx.NewsEntries.OfKey(first.Key)));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _bed.Write())
        {
            Assert.True(await ctx.HasDeletedRowsAsync(ctx.NewsEntries.OfKey(first.Key)));
            Assert.Equal(0, await ctx.MarkSoftDeletedAsync(ctx.NewsEntries.OfKey(first.Key)));
            Assert.Equal(2, await ctx.MarkRestoredAsync(ctx.NewsEntries.OfKey(first.Key)));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _bed.Write())
        {
            Assert.Equal(2, (await ctx.NewsEntries.OfKey(first.Key).ToListAsync()).Count);
            await ctx.MarkSoftDeletedAsync(ctx.NewsEntries.OfKey(first.Key));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _bed.Write())
        {
            Assert.Equal(2, await ctx.MarkHardDeletedAsync(ctx.NewsEntries.OfKey(first.Key)));
            await ctx.SaveChangesAsync();
        }

        await using var read = _bed.Read();
        using var scope = read.SoftDelete.IncludeDeleted();
        Assert.Empty(await read.NewsEntries.OfKey(first.Key).ToListAsync());
    }

    [Fact]
    public async Task LiveIn_SelectsTheRowsWhoseVersionIsTheActiveOne()
    {
        var first = await _bed.SaveNewsAsync(null, "Live");
        await _bed.SaveNewsAsync(first.Key, "Draft", publish: false);
        await _bed.CreateNewsAsync("Never live", publish: false);
        await using var ctx = _bed.Read();

        var live = await ctx.NewsEntries.LiveIn(ctx.ActiveVersions, News.EntityType).ToListAsync();

        Assert.Equal(["Live"], live.Select(Title));
    }
}
