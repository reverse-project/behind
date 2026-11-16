using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class NewsCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private sealed record Stored(
        long Version,
        string Title,
        string Summary,
        string Content,
        Guid Author,
        Guid? Category,
        bool Highlighted,
        DateTimeOffset? Start,
        DateTimeOffset? End);

    private async Task<IReadOnlyList<Stored>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();

        return await ctx.NewsEntries
            .OfKey(key)
            .OrderBy(news => EF.Property<long>(news, VersionedColumns.Version))
            .Select(news => new Stored(
                EF.Property<long>(news, VersionedColumns.Version),
                news.Title,
                news.Summary,
                news.Content,
                EF.Property<Guid>(news, NewsConfiguration.AuthorIdProperty),
                EF.Property<Guid?>(news, NewsConfiguration.CategoryIdProperty),
                news.Highlighted,
                EF.Property<DateTimeOffset?>(news, NewsConfiguration.StartAtProperty),
                EF.Property<DateTimeOffset?>(news, NewsConfiguration.EndAtProperty)))
            .ToListAsync();
    }

    private async Task<long?> ActiveVersionAsync(Guid key)
    {
        await using var versions = _bed.ReadVersions();
        var active = await versions.ActiveVersionsAsync(News.EntityType, [key.ToString()]);

        return active.TryGetValue(key.ToString(), out var version) ? version : null;
    }

    private async Task<IReadOnlyList<string>> HighlightedLiveTitlesAsync()
    {
        await using var ctx = _bed.Read();
        var live = await ctx.ActiveVersions.Where(version => version.EntityType == News.EntityType).ToListAsync();

        var rows = await ctx.NewsEntries.Where(news => news.Highlighted).ToListAsync();

        return rows
            .Where(news => live.Any(version =>
                version.EntityKey == ctx.Entry(news).Property<Guid>(VersionedColumns.Key).CurrentValue.ToString()
                && version.Version == ctx.Entry(news).Property<long>(VersionedColumns.Version).CurrentValue))
            .Select(news => news.Title)
            .ToList();
    }

    private static INewsCommands.NewsDraft Draft(
        string title = "Headline",
        Option<Guid> category = default,
        bool highlighted = false,
        Option<DateTimeOffset> start = default,
        Option<DateTimeOffset> end = default) =>
        new(title, "<p>short</p>", "<p>long</p>", category, highlighted, start, end);

    [Fact]
    public async Task Create_Publish_StoresVersionOneAndMakesItLive()
    {
        var key = await _bed.CreateNewsAsync("Launch", authorId: _bed.OtherAuthorId);

        var stored = Assert.Single(await VersionsAsync(key));
        Assert.Equal(
            (1L, "Launch", "<p>short</p>", "<p>long</p>", _bed.OtherAuthorId, (Guid?)null, false, (DateTimeOffset?)null, (DateTimeOffset?)null),
            (stored.Version, stored.Title, stored.Summary, stored.Content, stored.Author, stored.Category, stored.Highlighted, stored.Start, stored.End));
        Assert.Equal(1L, await ActiveVersionAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("News", key));
        Assert.Equal(new AuditTimes(Some(NewsTestBed.Now), None), await _bed.TimesAsync<News>(key, 1));
    }

    [Fact]
    public async Task Create_Draft_StoresVersionOneWithoutMakingItLive()
    {
        var key = await _bed.CreateNewsAsync(publish: false);

        Assert.Single(await VersionsAsync(key));
        Assert.Null(await ActiveVersionAsync(key));
    }

    [Fact]
    public async Task Create_WithCategory_LinksTheCategoryKey()
    {
        var category = await _bed.CreateCategoryAsync();

        var key = await _bed.CreateNewsAsync(categoryId: category);

        Assert.Equal(category, Assert.Single(await VersionsAsync(key)).Category);
    }

    [Fact]
    public async Task Create_WithUnknownOrDeletedCategory_IsRejectedAndStoresNothing()
    {
        var deleted = await _bed.CreateCategoryAsync("Gone");
        await using (var ctx = _bed.Write())
        {
            await new NewsCategoryCommands(ctx, Substitute.For<IVfsCommands>()).Delete(deleted);
        }

        foreach (var category in new[] { Guid.NewGuid(), deleted })
        {
            var result = await _bed.WithNewsCommandsAsync(commands =>
                commands.Create(_bed.AuthorId, Draft(category: category), true));

            Assert.IsType<NewsSaveResult.UnknownCategory>(result);
        }

        await using var read = _bed.Read();
        Assert.Empty(await read.NewsEntries.ToListAsync());
    }

    [Fact]
    public async Task Create_StoresTheScheduleWhenThereIsOneAndHasNoScheduleByDefault()
    {
        var start = NewsTestBed.Now.AddDays(1);
        var timed = await _bed.CreateNewsAsync("Timed", startAt: start, endAt: start.AddDays(2));
        var startOnly = await _bed.CreateNewsAsync("Start only", startAt: start);
        var plain = await _bed.CreateNewsAsync("Plain");

        var stored = Assert.Single(await VersionsAsync(timed));
        Assert.Equal(((DateTimeOffset?)start, (DateTimeOffset?)start.AddDays(2)), (stored.Start, stored.End));
        var open = Assert.Single(await VersionsAsync(startOnly));
        Assert.Equal(((DateTimeOffset?)start, (DateTimeOffset?)null), (open.Start, open.End));
        var unscheduled = Assert.Single(await VersionsAsync(plain));
        Assert.Equal(((DateTimeOffset?)null, (DateTimeOffset?)null), (unscheduled.Start, unscheduled.End));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_EndNotAfterTheStart_IsRejected(int hours)
    {
        var result = await _bed.WithNewsCommandsAsync(commands => commands.Create(
            _bed.AuthorId,
            Draft(start: NewsTestBed.Now, end: NewsTestBed.Now.AddHours(hours)),
            true));

        Assert.IsType<NewsSaveResult.InvalidSchedule>(result);
        await using var read = _bed.Read();
        Assert.Empty(await read.NewsEntries.ToListAsync());
    }

    [Fact]
    public async Task Create_AnEndWithoutAStart_IsRejected()
    {
        var result = await _bed.WithNewsCommandsAsync(commands => commands.Create(
            _bed.AuthorId,
            Draft(end: NewsTestBed.Now.AddHours(1)),
            true));

        Assert.IsType<NewsSaveResult.InvalidSchedule>(result);
    }

    [Fact]
    public async Task SaveVersion_CanAddAndRemoveTheScheduleInANewVersion()
    {
        var first = await _bed.SaveNewsAsync(null);
        var start = NewsTestBed.Now.AddDays(1);

        await _bed.SaveNewsAsync(first.Key, startAt: start, endAt: start.AddDays(1));
        await _bed.SaveNewsAsync(first.Key);

        var versions = await VersionsAsync(first.Key);
        Assert.Equal(
            [((DateTimeOffset?)null, (DateTimeOffset?)null), ((DateTimeOffset?)start, (DateTimeOffset?)start.AddDays(1)), ((DateTimeOffset?)null, (DateTimeOffset?)null)],
            versions.Select(version => (version.Start, version.End)));
    }

    [Fact]
    public async Task SaveVersion_InsertsTheNextVersionAndNeverTouchesTheOldOne()
    {
        var first = await _bed.SaveNewsAsync(null, "First", authorId: _bed.OtherAuthorId);

        var second = await _bed.SaveNewsAsync(first.Key, "Second", at: NewsTestBed.Now.AddHours(1), authorId: _bed.AuthorId);

        Assert.Equal(new VersionedKey<Guid>(first.Key, 2), second);
        var versions = await VersionsAsync(first.Key);
        Assert.Equal(["First", "Second"], versions.Select(version => version.Title));
        Assert.Equal([_bed.OtherAuthorId, _bed.OtherAuthorId], versions.Select(version => version.Author));
        Assert.Equal(["Created"], await _bed.ActionsAsync("News", first.Key, 1));
        Assert.Equal(["Created"], await _bed.ActionsAsync("News", first.Key, 2));
    }

    [Fact]
    public async Task SaveVersion_Publish_MakesTheNewVersionLive()
    {
        var first = await _bed.SaveNewsAsync(null);

        await _bed.SaveNewsAsync(first.Key, publish: true);

        Assert.Equal(2L, await ActiveVersionAsync(first.Key));
    }

    [Fact]
    public async Task SaveVersion_Draft_KeepsTheLiveVersion()
    {
        var first = await _bed.SaveNewsAsync(null);

        await _bed.SaveNewsAsync(first.Key, publish: false);

        Assert.Equal(1L, await ActiveVersionAsync(first.Key));
        Assert.Equal(2, (await VersionsAsync(first.Key)).Count);
    }

    [Fact]
    public async Task SaveVersion_TheUpdatedTimeIsTheCreationOfTheShownVersion()
    {
        var first = await _bed.SaveNewsAsync(null, at: NewsTestBed.Now);
        await _bed.SaveNewsAsync(first.Key, at: NewsTestBed.Now.AddHours(3));

        Assert.Equal(
            new AuditTimes(Some(NewsTestBed.Now), Some(NewsTestBed.Now.AddHours(3))),
            await _bed.TimesAsync<News>(first.Key, 2));
        Assert.Equal(new AuditTimes(Some(NewsTestBed.Now), None), await _bed.TimesAsync<News>(first.Key, 1));
    }

    [Fact]
    public async Task SaveVersion_UnknownOrDeletedNews_ReturnsNotFound()
    {
        var key = await _bed.CreateNewsAsync();
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(key));

        Assert.IsType<NewsSaveResult.NotFound>(
            await _bed.WithNewsCommandsAsync(commands => commands.SaveVersion(key, Draft(), true)));
        Assert.IsType<NewsSaveResult.NotFound>(
            await _bed.WithNewsCommandsAsync(commands => commands.SaveVersion(Guid.NewGuid(), Draft(), true)));
    }

    [Fact]
    public async Task SaveVersion_InvalidCategoryOrSchedule_InsertsNothing()
    {
        var key = await _bed.CreateNewsAsync();

        var category = await _bed.WithNewsCommandsAsync(commands =>
            commands.SaveVersion(key, Draft(category: Guid.NewGuid()), true));
        var schedule = await _bed.WithNewsCommandsAsync(commands =>
            commands.SaveVersion(key, Draft(start: NewsTestBed.Now, end: NewsTestBed.Now), true));

        Assert.IsType<NewsSaveResult.UnknownCategory>(category);
        Assert.IsType<NewsSaveResult.InvalidSchedule>(schedule);
        Assert.Single(await VersionsAsync(key));
    }

    [Fact]
    public async Task Activate_SwitchesTheLiveVersionIncludingBackToAnOlderOne()
    {
        var first = await _bed.SaveNewsAsync(null, "First");
        var second = await _bed.SaveNewsAsync(first.Key, "Second");

        var result = await _bed.WithNewsCommandsAsync(commands => commands.Activate(first));

        Assert.Equal(first, Assert.IsType<NewsSaveResult.Saved>(result).Id);
        Assert.Equal(1L, await ActiveVersionAsync(first.Key));
        Assert.Equal(2L, second.Version);
    }

    [Fact]
    public async Task Activate_MakesADraftLive()
    {
        var draft = await _bed.SaveNewsAsync(null, publish: false);

        await _bed.WithNewsCommandsAsync(commands => commands.Activate(draft));

        Assert.Equal(1L, await ActiveVersionAsync(draft.Key));
    }

    [Fact]
    public async Task Activate_UnknownVersion_ReturnsNotFound()
    {
        var key = await _bed.CreateNewsAsync();

        Assert.IsType<NewsSaveResult.NotFound>(
            await _bed.WithNewsCommandsAsync(commands => commands.Activate(new VersionedKey<Guid>(key, 9))));
        Assert.IsType<NewsSaveResult.NotFound>(
            await _bed.WithNewsCommandsAsync(commands => commands.Activate(new VersionedKey<Guid>(Guid.NewGuid(), 1))));
    }

    [Fact]
    public async Task Highlight_PublishingAHighlightedVersionRemovesTheHighlightOfTheOtherLiveNews()
    {
        await _bed.CreateNewsAsync("First", highlighted: true);
        await _bed.CreateNewsAsync("Second", highlighted: true);

        Assert.Equal(["Second"], await HighlightedLiveTitlesAsync());
    }

    [Fact]
    public async Task Highlight_AHighlightedDraftDoesNotChangeTheLiveHighlight()
    {
        await _bed.CreateNewsAsync("First", highlighted: true);
        await _bed.CreateNewsAsync("Draft", highlighted: true, publish: false);

        Assert.Equal(["First"], await HighlightedLiveTitlesAsync());
    }

    [Fact]
    public async Task Highlight_ActivatingAHighlightedDraftMovesTheHighlight()
    {
        await _bed.CreateNewsAsync("First", highlighted: true);
        var draft = await _bed.SaveNewsAsync(null, "Draft", highlighted: true, publish: false);

        await _bed.WithNewsCommandsAsync(commands => commands.Activate(draft));

        Assert.Equal(["Draft"], await HighlightedLiveTitlesAsync());
    }

    [Fact]
    public async Task Highlight_PublishingANotHighlightedVersionRemovesTheHighlightOfThatNews()
    {
        var first = await _bed.SaveNewsAsync(null, "First", highlighted: true);

        await _bed.SaveNewsAsync(first.Key, "First", highlighted: false);

        Assert.Empty(await HighlightedLiveTitlesAsync());
    }

    [Fact]
    public async Task Highlight_NotHighlightedNewsLeavesTheCurrentHighlightAlone()
    {
        await _bed.CreateNewsAsync("First", highlighted: true);
        await _bed.CreateNewsAsync("Second");

        Assert.Equal(["First"], await HighlightedLiveTitlesAsync());
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersionAndTheCommentsAndClearsTheHighlight()
    {
        var first = await _bed.SaveNewsAsync(null, highlighted: true);
        await _bed.SaveNewsAsync(first.Key, publish: false);
        var comment = await _bed.AddCommentAsync(first.Key);

        Assert.True(await _bed.WithNewsCommandsAsync(commands => commands.Delete(first.Key)));

        await using var read = _bed.Read();
        Assert.Empty(await read.NewsEntries.ToListAsync());
        Assert.Empty(await read.NewsComments.ToListAsync());
        Assert.Equal(["Created", "Deleted"], (await _bed.ActionsAsync("News", first.Key, 1)).Where(action => action != "Updated"));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("News", first.Key, 2));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("NewsComment", comment));
        Assert.Empty(await HighlightedLiveTitlesAsync());
    }

    [Fact]
    public async Task Delete_UnknownOrAlreadyDeletedNews_ReturnsFalse()
    {
        var key = await _bed.CreateNewsAsync();
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(key));

        Assert.False(await _bed.WithNewsCommandsAsync(commands => commands.Delete(key)));
        Assert.False(await _bed.WithNewsCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsBackEveryVersionTheLiveVersionAndTheComments()
    {
        var first = await _bed.SaveNewsAsync(null);
        await _bed.SaveNewsAsync(first.Key);
        var comment = await _bed.AddCommentAsync(first.Key);
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(first.Key));

        Assert.True(await _bed.WithNewsCommandsAsync(commands => commands.Restore(first.Key)));

        await using var read = _bed.Read();
        Assert.Equal(2, (await read.NewsEntries.ToListAsync()).Count);
        Assert.Single(await read.NewsComments.ToListAsync());
        Assert.Equal(2L, await ActiveVersionAsync(first.Key));
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("NewsComment", comment));
    }

    [Fact]
    public async Task Restore_DoesNotBringBackTheHighlightSoThereIsNeverASecondOne()
    {
        var first = await _bed.CreateNewsAsync("First", highlighted: true);
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(first));
        await _bed.CreateNewsAsync("Second", highlighted: true);

        await _bed.WithNewsCommandsAsync(commands => commands.Restore(first));

        Assert.Equal(["Second"], await HighlightedLiveTitlesAsync());
    }

    [Fact]
    public async Task Restore_ActiveOrUnknownNews_ReturnsFalse()
    {
        var key = await _bed.CreateNewsAsync();

        Assert.False(await _bed.WithNewsCommandsAsync(commands => commands.Restore(key)));
        Assert.False(await _bed.WithNewsCommandsAsync(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task HardDelete_RemovesDeletedNewsWithItsVersionsCommentsAndLiveVersion()
    {
        var first = await _bed.SaveNewsAsync(null);
        await _bed.SaveNewsAsync(first.Key);
        await _bed.AddCommentAsync(first.Key);
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(first.Key));

        var result = await _bed.WithNewsCommandsAsync(commands => commands.HardDelete(first.Key));

        Assert.Equal(HardDeleteResult.Deleted, result);
        Assert.Empty(await VersionsAsync(first.Key));
        await using var read = _bed.Read();
        using var scope = read.SoftDelete.IncludeDeleted();
        Assert.Empty(await read.NewsComments.ToListAsync());
        Assert.Null(await ActiveVersionAsync(first.Key));
        Assert.Contains("HardDeleted", await _bed.ActionsAsync("News", first.Key, 1));
    }

    [Fact]
    public async Task HardDelete_ActiveOrUnknownNews_IsNotFound()
    {
        var key = await _bed.CreateNewsAsync();

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithNewsCommandsAsync(commands => commands.HardDelete(key)));
        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithNewsCommandsAsync(commands => commands.HardDelete(Guid.NewGuid())));
        Assert.Single(await VersionsAsync(key));
    }
}
