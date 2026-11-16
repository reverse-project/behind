using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class CarouselCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private sealed record StoredSlide(long Version, string Caption, string Link, Guid Image, int Position);

    private async Task<IReadOnlyList<(long Version, int Interval)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();

        var rows = await ctx.Carousels
            .OfKey(key)
            .OrderBy(carousel => EF.Property<long>(carousel, VersionedColumns.Version))
            .Select(carousel => new { Version = EF.Property<long>(carousel, VersionedColumns.Version), carousel.IntervalSeconds })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.IntervalSeconds)).ToList();
    }

    private async Task<IReadOnlyList<StoredSlide>> SlidesAsync(Guid key)
    {
        await using var ctx = _bed.Read();

        return await ctx.CarouselSlides
            .Where(slide => EF.Property<Guid>(slide, CarouselSlideConfiguration.CarouselIdProperty) == key)
            .OrderBy(slide => EF.Property<long>(slide, VersionedColumns.Version))
            .ThenBy(slide => EF.Property<int>(slide, CarouselSlideConfiguration.PositionProperty))
            .Select(slide => new StoredSlide(
                EF.Property<long>(slide, VersionedColumns.Version),
                slide.Caption,
                slide.LinkUrl,
                EF.Property<Guid>(slide, CarouselSlideConfiguration.ImageIdProperty),
                EF.Property<int>(slide, CarouselSlideConfiguration.PositionProperty)))
            .ToListAsync();
    }

    private async Task<long?> ActiveVersionAsync(Guid key)
    {
        await using var versions = _bed.ReadVersions();

        return await versions.ActiveVersions
            .Where(active => active.EntityType == Carousel.EntityType && active.EntityKey == key.ToString())
            .Select(active => (long?)active.Version)
            .SingleOrDefaultAsync();
    }

    private async Task<bool> ExistsAsync(string path) => (await _bed.ContentStore.Read(path)).Match(() => false, _ => true);

    [Fact]
    public async Task Save_CreatesTheFirstVersionKeyedBySiteWithSlidesInOrderAndMakesItLive()
    {
        var siteId = await _bed.CreateSiteAsync();
        var draft = await _bed.NewCarouselDraftAsync(7, "One", "Two", "Three");

        var id = await _bed.SaveCarouselAsync(siteId, draft);

        Assert.Equal(new VersionedKey<Guid>(siteId, 1), id);
        Assert.Equal([(1L, 7)], await VersionsAsync(siteId));
        Assert.Equal(["One", "Two", "Three"], (await SlidesAsync(siteId)).Select(slide => slide.Caption));
        Assert.Equal([0, 1, 2], (await SlidesAsync(siteId)).Select(slide => slide.Position));
        Assert.Equal(1, await ActiveVersionAsync(siteId));
    }

    [Fact]
    public async Task Save_InsertsTheNextVersionAndNeverUpdatesTheEarlierOne()
    {
        var siteId = await _bed.CreateSiteAsync();
        await _bed.SaveCarouselAsync(siteId, await _bed.NewCarouselDraftAsync(5, "Old"));

        var second = await _bed.SaveCarouselAsync(siteId, await _bed.NewCarouselDraftAsync(9, "New"));

        Assert.Equal(2, second.Version);
        Assert.Equal([(1L, 5), (2L, 9)], await VersionsAsync(siteId));
        Assert.Equal(["Old", "New"], (await SlidesAsync(siteId)).Select(slide => slide.Caption));
        Assert.Equal(2, await ActiveVersionAsync(siteId));
    }

    [Fact]
    public async Task Save_UnknownSite_ReturnsNotFound()
    {
        var draft = await _bed.NewCarouselDraftAsync();

        var result = await _bed.WithCarouselCommandsAsync(commands => commands.Save(Guid.NewGuid(), draft));

        Assert.IsType<CarouselSaveResult.NotFound>(result);
    }

    [Fact]
    public async Task Save_ImageOfAnotherKindOrUnknown_ReturnsUnknownImageAndStoresNothing()
    {
        var siteId = await _bed.CreateSiteAsync();
        var siteImage = await _bed.AddImageAsync();

        var foreign = await _bed.WithCarouselCommandsAsync(commands => commands.Save(
            siteId,
            new ICarouselCommands.CarouselDraft(5, [new ICarouselCommands.CarouselSlideDraft(siteImage, "x", string.Empty)])));
        var unknown = await _bed.WithCarouselCommandsAsync(commands => commands.Save(
            siteId,
            new ICarouselCommands.CarouselDraft(5, [new ICarouselCommands.CarouselSlideDraft(Guid.NewGuid(), "x", string.Empty)])));

        Assert.IsType<CarouselSaveResult.UnknownImage>(foreign);
        Assert.IsType<CarouselSaveResult.UnknownImage>(unknown);
        Assert.Empty(await VersionsAsync(siteId));
    }

    [Fact]
    public async Task Save_MovesTheImagesUnderTheSite()
    {
        var siteId = await _bed.CreateSiteAsync();
        var draft = await _bed.NewCarouselDraftAsync(5, "One");
        var image = draft.Slides[0].ImageId;

        await _bed.SaveCarouselAsync(siteId, draft);

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Carousels, image, siteId)));
        Assert.False(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Carousels, image)));
    }

    [Fact]
    public async Task Save_SameImageTwiceInOneCarousel_IsAccepted()
    {
        var siteId = await _bed.CreateSiteAsync();
        var image = await _bed.AddCarouselImageAsync();

        var result = await _bed.WithCarouselCommandsAsync(commands => commands.Save(
            siteId,
            new ICarouselCommands.CarouselDraft(
                5,
                [
                    new ICarouselCommands.CarouselSlideDraft(image, "a", string.Empty),
                    new ICarouselCommands.CarouselSlideDraft(image, "b", "#/about")
                ])));

        Assert.IsType<CarouselSaveResult.Saved>(result);
        Assert.Equal(["#/about"], (await SlidesAsync(siteId)).Select(slide => slide.Link).Where(link => link != string.Empty));
    }

    [Fact]
    public async Task Delete_SoftDeletesAllVersionsAndRestoreBringsThemBack()
    {
        var siteId = await _bed.CreateSiteAsync();
        await _bed.SaveCarouselAsync(siteId);

        Assert.True(await _bed.WithCarouselCommandsAsync(commands => commands.Delete(siteId)));
        Assert.False(await _bed.WithCarouselCommandsAsync(commands => commands.Delete(siteId)));
        Assert.True((await _bed.WithCarouselQueriesAsync(queries => queries.FindLive(siteId))).IsNone());

        Assert.True(await _bed.WithCarouselCommandsAsync(commands => commands.Restore(siteId)));
        Assert.False(await _bed.WithCarouselCommandsAsync(commands => commands.Restore(siteId)));
        Assert.False((await _bed.WithCarouselQueriesAsync(queries => queries.FindLive(siteId))).IsNone());
    }

    [Fact]
    public async Task Save_AfterADelete_InsertsANewVersionBesideTheDeletedOnes()
    {
        var siteId = await _bed.CreateSiteAsync();
        await _bed.SaveCarouselAsync(siteId);
        await _bed.WithCarouselCommandsAsync(commands => commands.Delete(siteId));

        var id = await _bed.SaveCarouselAsync(siteId);

        Assert.Equal(2, id.Version);
        Assert.Equal(2, await ActiveVersionAsync(siteId));
    }

    [Fact]
    public async Task HardDelete_OnlyRemovesDeletedCarouselsTheirSlidesAndUnusedImages()
    {
        var siteId = await _bed.CreateSiteAsync();
        var draft = await _bed.NewCarouselDraftAsync(5, "One");
        var image = draft.Slides[0].ImageId;
        await _bed.SaveCarouselAsync(siteId, draft);

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithCarouselCommandsAsync(commands => commands.HardDelete(siteId)));
        Assert.NotEmpty(await SlidesAsync(siteId));

        await _bed.WithCarouselCommandsAsync(commands => commands.Delete(siteId));

        Assert.Equal(HardDeleteResult.Deleted, await _bed.WithCarouselCommandsAsync(commands => commands.HardDelete(siteId)));
        Assert.Empty(await VersionsAsync(siteId));
        Assert.Empty(await SlidesAsync(siteId));
        Assert.Null(await ActiveVersionAsync(siteId));
        Assert.False(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Carousels, image, siteId)));
    }

    [Fact]
    public async Task HardDelete_KeepsImagesStillUsedByALiveVersion()
    {
        var siteId = await _bed.CreateSiteAsync();
        var draft = await _bed.NewCarouselDraftAsync(5, "One");
        var image = draft.Slides[0].ImageId;
        await _bed.SaveCarouselAsync(siteId, draft);
        await _bed.WithCarouselCommandsAsync(commands => commands.Delete(siteId));
        await _bed.SaveCarouselAsync(siteId, draft);

        await _bed.WithCarouselCommandsAsync(commands => commands.HardDelete(siteId));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Carousels, image, siteId)));
        Assert.Equal([(2L, 5)], await VersionsAsync(siteId));
        Assert.Equal(2, await ActiveVersionAsync(siteId));
    }

    [Fact]
    public async Task SiteCommands_DeleteRestoreAndHardDelete_FollowTheSiteCarousel()
    {
        var siteId = await _bed.CreateSiteAsync();
        await _bed.SaveCarouselAsync(siteId);

        Assert.True(await _bed.WithSiteCommandsAsync(commands => commands.Delete(siteId)));

        Assert.True((await _bed.WithCarouselQueriesAsync(queries => queries.FindManaged(siteId, DeletionState.Active))).IsNone());
        Assert.False((await _bed.WithCarouselQueriesAsync(queries => queries.FindManaged(siteId, DeletionState.Deleted))).IsNone());

        Assert.IsType<SiteSaveResult.Saved>(await _bed.WithSiteCommandsAsync(commands => commands.Restore(siteId)));

        Assert.False((await _bed.WithCarouselQueriesAsync(queries => queries.FindManaged(siteId, DeletionState.Active))).IsNone());

        await _bed.WithSiteCommandsAsync(commands => commands.Delete(siteId));

        Assert.Equal(HardDeleteResult.Deleted, await _bed.WithSiteCommandsAsync(commands => commands.HardDelete(siteId)));
        Assert.Empty(await VersionsAsync(siteId));
        Assert.Empty(await SlidesAsync(siteId));
    }
}
