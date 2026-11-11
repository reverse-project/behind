using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class AuditTimeExtensionsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task AuditTimesAsync_NoEntities_ReturnsNothing()
    {
        await using var ctx = _bed.Read();

        Assert.Empty(await ctx.AuditTimesAsync<News>(new Dictionary<Guid, long>()));
    }

    [Fact]
    public async Task AuditTimesAsync_CreatedIsVersionOneAndUpdatedIsTheCreationOfTheShownLaterVersion()
    {
        var first = await _bed.SaveNewsAsync(null, "First", at: NewsTestBed.Now);
        await _bed.SaveNewsAsync(first.Key, "First 2", at: NewsTestBed.Now.AddHours(1));
        await _bed.SaveNewsAsync(first.Key, "First 3", at: NewsTestBed.Now.AddHours(2));
        var second = await _bed.CreateNewsAsync("Second", at: NewsTestBed.Now.AddDays(1));

        await using var read = _bed.Read();
        var times = await read.AuditTimesAsync<News>(new Dictionary<Guid, long>
        {
            [first.Key] = 2,
            [second] = 1,
            [Guid.NewGuid()] = 1
        });

        Assert.Equal(3, times.Count);
        Assert.Equal(new AuditTimes(Some(NewsTestBed.Now), Some(NewsTestBed.Now.AddHours(1))), times[first.Key]);
        Assert.Equal(new AuditTimes(Some(NewsTestBed.Now.AddDays(1)), None), times[second]);
    }

    [Fact]
    public async Task AuditTimesAsync_VersionOneShownHasNoUpdatedTime()
    {
        var first = await _bed.SaveNewsAsync(null, at: NewsTestBed.Now);
        await _bed.SaveNewsAsync(first.Key, at: NewsTestBed.Now.AddHours(1));

        Assert.Equal(new AuditTimes(Some(NewsTestBed.Now), None), await _bed.TimesAsync<News>(first.Key, 1));
    }

    [Fact]
    public async Task AuditTimesAsync_OnlyLooksAtTheRequestedEntityType()
    {
        var id = await _bed.CreateNewsAsync();
        await using var ctx = _bed.Read();

        var times = await ctx.AuditTimesAsync<NewsCategory>(new Dictionary<Guid, long> { [id] = 1 });

        Assert.Equal(new AuditTimes(None, None), times[id]);
    }

    [Fact]
    public async Task VersionCreationTimesAsync_ReturnsTheCreationTimeOfEachVersion()
    {
        var first = await _bed.SaveNewsAsync(null, at: NewsTestBed.Now);
        await _bed.SaveNewsAsync(first.Key, at: NewsTestBed.Now.AddHours(1));
        await using var ctx = _bed.Read();

        var times = await ctx.VersionCreationTimesAsync<News>(first.Key, [1, 2, 3]);

        Assert.Equal(
            new Dictionary<long, DateTimeOffset> { [1] = NewsTestBed.Now, [2] = NewsTestBed.Now.AddHours(1) },
            times);
    }

    [Fact]
    public async Task OrderByCreation_FollowsTheCreationOfTheFirstVersionInBothDirections()
    {
        var middle = await _bed.CreateNewsAsync("Middle", at: NewsTestBed.Now.AddHours(1));
        var oldest = await _bed.CreateNewsAsync("Oldest", at: NewsTestBed.Now);
        await _bed.CreateNewsAsync("Newest", at: NewsTestBed.Now.AddHours(2));
        await _bed.SaveNewsAsync(oldest, "Oldest edited", at: NewsTestBed.Now.AddDays(5));
        await using var ctx = _bed.Read();
        var latestOnly = ctx.NewsEntries.LatestOf(ctx.NewsEntries);

        var descending = await ctx.OrderByCreation(latestOnly, newestFirst: true).Select(news => news.Title).ToListAsync();
        var ascending = await ctx.OrderByCreation(latestOnly, newestFirst: false).Select(news => news.Title).ToListAsync();

        Assert.Equal(["Newest", "Middle", "Oldest edited"], descending);
        Assert.Equal(["Oldest edited", "Middle", "Newest"], ascending);
        Assert.NotEqual(middle, oldest);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreationOrdering_EqualOverridesUseAuditCreationBeforeKey(bool newestFirst)
    {
        async Task Add(Guid key, string title, DateTimeOffset at)
        {
            await using var context = _bed.Write(at);
            var news = new News(title, "", "", false);
            await context.NewsEntries.AddAsync(news);
            context.Entry(news).StampVersionedKey(key, 1);
            context.Entry(news).Property<Guid>(NewsConfiguration.AuthorIdProperty).CurrentValue = _bed.AuthorId;
            context.Entry(news).Property<DateTimeOffset?>(NewsConfiguration.StartAtProperty).CurrentValue = NewsTestBed.Now;
            await context.SaveChangesAsync();
        }

        await Add(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Newer", NewsTestBed.Now.AddHours(1));
        await Add(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), "Older", NewsTestBed.Now);
        await using var read = _bed.Read();
        var rows = read.NewsEntries;
        var expected = newestFirst ? new[] { "Newer", "Older" } : ["Older", "Newer"];
        var ordered = await read.OrderByCreation(rows, newestFirst,
                news => EF.Property<DateTimeOffset?>(news, NewsConfiguration.StartAtProperty))
            .Select(news => news.Title).ToListAsync();
        var thenOrdered = await read.ThenByCreation(rows.OrderBy(news => news.Highlighted), newestFirst,
                news => EF.Property<DateTimeOffset?>(news, NewsConfiguration.StartAtProperty))
            .Select(news => news.Title).ToListAsync();

        Assert.Equal(expected, ordered);
        Assert.Equal(expected, thenOrdered);
    }
}
