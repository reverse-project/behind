using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class CarouselQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task FindLive_ReturnsTheNewestVersionWithItsSlidesInOrder()
    {
        var siteId = await _bed.CreateSiteAsync();
        await _bed.SaveCarouselAsync(siteId, await _bed.NewCarouselDraftAsync(4, "Old"));
        await _bed.SaveCarouselAsync(siteId, await _bed.NewCarouselDraftAsync(8, "A", "B"));

        var found = await _bed.WithCarouselQueriesAsync(queries => queries.FindLive(siteId));

        var carousel = found.Match(() => throw new InvalidOperationException(), value => value);
        Assert.Equal(new VersionedKey<Guid>(siteId, 2), carousel.Id);
        Assert.Equal(8, carousel.IntervalSeconds);
        Assert.Equal(["A", "B"], carousel.Slides.Select(slide => slide.Caption));
        Assert.Equal([0, 1], carousel.Slides.Select(slide => slide.Position));
    }

    [Fact]
    public async Task FindLive_SiteWithoutCarousel_ReturnsNone()
    {
        var siteId = await _bed.CreateSiteAsync();

        Assert.True((await _bed.WithCarouselQueriesAsync(queries => queries.FindLive(siteId))).IsNone());
    }

    [Fact]
    public async Task FindLive_DraftSite_ReturnsNone()
    {
        var siteId = await _bed.CreateSiteAsync(publish: false);
        await _bed.SaveCarouselAsync(siteId);

        Assert.True((await _bed.WithCarouselQueriesAsync(queries => queries.FindLive(siteId))).IsNone());
    }

    [Fact]
    public async Task FindLive_DeletedSite_ReturnsNone()
    {
        var siteId = await _bed.CreateSiteAsync();
        await _bed.SaveCarouselAsync(siteId);
        await _bed.WithSiteCommandsAsync(commands => commands.Delete(siteId));

        Assert.True((await _bed.WithCarouselQueriesAsync(queries => queries.FindLive(siteId))).IsNone());
    }

    [Fact]
    public async Task FindManaged_ReturnsTheLatestCarouselOfTheRequestedDeletionState()
    {
        var siteId = await _bed.CreateSiteAsync();
        await _bed.SaveCarouselAsync(siteId, await _bed.NewCarouselDraftAsync(5, "Kept"));

        Assert.False((await _bed.WithCarouselQueriesAsync(queries => queries.FindManaged(siteId, DeletionState.Active))).IsNone());
        Assert.True((await _bed.WithCarouselQueriesAsync(queries => queries.FindManaged(siteId, DeletionState.Deleted))).IsNone());

        await _bed.WithCarouselCommandsAsync(commands => commands.Delete(siteId));

        Assert.True((await _bed.WithCarouselQueriesAsync(queries => queries.FindManaged(siteId, DeletionState.Active))).IsNone());
        var deleted = await _bed.WithCarouselQueriesAsync(queries => queries.FindManaged(siteId, DeletionState.Deleted));
        Assert.Equal(["Kept"], deleted.Match(() => [], value => value.Slides.Select(slide => slide.Caption).ToList()));
    }
}
