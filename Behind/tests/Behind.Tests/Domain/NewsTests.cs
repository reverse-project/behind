using Behind.Core.Database;

namespace Behind.Tests.Domain;

public class NewsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void News_KeepsItsFields()
    {
        var id = new VersionedKey<Guid>(Guid.NewGuid(), 2);
        var news = new News(id, "Title", "<p>short</p>", "<p>long</p>", None, None);

        Assert.Equal(id, news.Id);

        Assert.Equal("Title", news.Title);
        Assert.Equal("<p>short</p>", news.Summary);
        Assert.Equal("<p>long</p>", news.Content);
        Assert.False(news.Highlighted);
        Assert.True(new News(id, "Title", "", "", Some(Start), None, true).Highlighted);
        Assert.Equal(0, new News("Title", "", "", false).Id.Version);
    }

    [Theory]
    [InlineData("<p>long</p>", "<p>long</p>")]
    [InlineData("", "<p>short</p>")]
    [InlineData("  \n ", "<p>short</p>")]
    public void DisplayContent_FallsBackToTheSummaryWhenThereIsNoContent(string content, string expected)
    {
        var detail = new INewsQueries.NewsDetail(
            new VersionedKey<Guid>(Guid.NewGuid(), 1), "Title", "<p>short</p>", content, "Ada", None, None, None, false, None, None);

        Assert.Equal(expected, detail.DisplayContent);
    }

    [Fact]
    public void EntityTypes_AreTheNamesUsedByTheAuditLog()
    {
        Assert.Equal("News", News.EntityType);
        Assert.Equal("NewsCategory", NewsCategory.EntityType);
        Assert.Equal(0, new NewsCategory("Name").Id.Version);
        Assert.Equal(0, new NewsComment("Hi").Id.Version);
        Assert.Equal("NewsComment", NewsComment.EntityType);
    }

    [Fact]
    public void NewsGrantArea_IsPartOfTheBuiltInAreas()
    {
        Assert.Contains(GrantArea.News, GrantArea.All);
        Assert.Contains("News.Write", BuiltInAccess.GrantNames);
    }

    [Theory]
    [InlineData(null, null, NewsStatus.Live)]
    [InlineData(null, 1, NewsStatus.Live)]
    [InlineData(null, 0, NewsStatus.Expired)]
    [InlineData(null, -1, NewsStatus.Expired)]
    [InlineData(1, null, NewsStatus.Scheduled)]
    [InlineData(1, 2, NewsStatus.Scheduled)]
    [InlineData(0, null, NewsStatus.Live)]
    [InlineData(-1, null, NewsStatus.Live)]
    [InlineData(-1, 1, NewsStatus.Live)]
    [InlineData(-2, 0, NewsStatus.Expired)]
    [InlineData(-2, -1, NewsStatus.Expired)]
    public void StatusAt_FollowsTheScheduleAndAnEmptyScheduleIsLive(int? startHours, int? endHours, NewsStatus expected)
    {
        var now = Start;
        var start = startHours is { } startOffset ? Some(now.AddHours(startOffset)) : None;
        var end = endHours is { } hours ? Some(now.AddHours(hours)) : None;

        Assert.Equal(expected, start.StatusAt(end, now));
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, 1, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, -1, false)]
    [InlineData(false, null, true)]
    [InlineData(false, 1, false)]
    public void IsValidSchedule_AnEndNeedsAStartBeforeIt(bool hasStart, int? endHours, bool expected)
    {
        var start = hasStart ? Some(Start) : None;
        var end = endHours is { } hours ? Some(Start.AddHours(hours)) : None;

        Assert.Equal(expected, start.IsValidSchedule(end));
    }
}
