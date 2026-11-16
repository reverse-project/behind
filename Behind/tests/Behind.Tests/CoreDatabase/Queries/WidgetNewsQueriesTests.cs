using Behind.Core.Database.Queries;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class WidgetNewsQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    [Fact]
    public async Task Latest_UsesAuditCreationOrderAndRetainsPublicVisibilityFilters()
    {
        var now = NewsTestBed.Now.AddDays(10);
        await _bed.CreateNewsAsync("Old featured", highlighted: true, at: NewsTestBed.Now);
        await _bed.CreateNewsAsync("New", at: NewsTestBed.Now.AddDays(1));
        await _bed.CreateNewsAsync("Draft", at: NewsTestBed.Now.AddDays(2), publish: false);
        await _bed.CreateNewsAsync("Future", at: NewsTestBed.Now.AddDays(3), startAt: now.AddDays(1));
        await _bed.CreateNewsAsync("Expired", at: NewsTestBed.Now.AddDays(4),
            startAt: NewsTestBed.Now, endAt: now.AddDays(-1));
        var deleted = await _bed.CreateNewsAsync("Deleted", at: NewsTestBed.Now.AddDays(5));
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(deleted));
        await using var context = _bed.Read();

        var latest = await new NewsQueries(context).Latest(50, now);

        Assert.Equal(["New", "Old featured"], latest.Select(item => item.Title));
        Assert.Equal("New", Assert.Single(await new NewsQueries(context).Latest(1, now)).Title);
    }

    public void Dispose() => _bed.Dispose();
}
