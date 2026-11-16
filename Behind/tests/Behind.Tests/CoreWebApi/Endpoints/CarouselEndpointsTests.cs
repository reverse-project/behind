using Behind.Common.WebApi;
using Behind.Common.Database;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class CarouselEndpointsTests
{
    private static readonly Guid SiteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ImageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static CarouselEndpoints.SaveCarouselRequest Request(
        int interval = 5,
        params CarouselEndpoints.SaveCarouselSlideRequest[] slides) =>
        new(interval, slides.Length == 0 ? [new CarouselEndpoints.SaveCarouselSlideRequest(ImageId, "Caption", null)] : slides);

    private static ICarouselQueries.CarouselDetail Detail(string link = "") =>
        new(
            new VersionedKey<Guid>(SiteId, 3),
            6,
            [new ICarouselQueries.CarouselSlideInfo(Guid.Parse("33333333-3333-3333-3333-333333333333"), ImageId, "Caption", link, 0)]);

    private static IOptionsSnapshot<CarouselsConfiguration> Options(CarouselsConfiguration? configuration = null) =>
        FixedOptionsSnapshot.Of(configuration ?? new CarouselsConfiguration());

    [Fact]
    public void GetSettings_ReportsTheConfiguredLimitsWithoutCaching()
    {
        var context = new DefaultHttpContext();
        var configuration = new CarouselsConfiguration
        {
            MaxSlides = 7,
            MinIntervalSeconds = 3,
            MaxIntervalSeconds = 30,
            IntervalSeconds = 9,
            MaxCaptionLength = 120,
            MaxImageBytes = 1024
        };

        var result = CarouselEndpoints.GetSettings(context, Options(configuration));

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(
            new CarouselSettingsResponse(7, 3, 30, 9, 120, CarouselSlide.MaxLinkUrlLength, 1024),
            Assert.IsType<JsonHttpResult<CarouselSettingsResponse>>(result).Value);
    }

    [Fact]
    public void GetSettings_WithoutConfiguration_ReportsTheDefaults()
    {
        var result = CarouselEndpoints.GetSettings(new DefaultHttpContext(), Options());

        Assert.Equal(
            new CarouselSettingsResponse(20, 2, 60, 5, 300, CarouselSlide.MaxLinkUrlLength, 5L * 1024 * 1024),
            Assert.IsType<JsonHttpResult<CarouselSettingsResponse>>(result).Value);
    }

    [Fact]
    public async Task GetPublic_ReturnsTheLiveCarouselWithImageUrlsAndNoCache()
    {
        var queries = Substitute.For<ICarouselQueries>();
        queries.FindLive(SiteId, Arg.Any<CancellationToken>()).Returns(Some(Detail("#/about")));
        var context = new DefaultHttpContext();

        var result = await CarouselEndpoints.GetPublic(SiteId, context, queries);

        var body = Assert.IsType<JsonHttpResult<CarouselResponse>>(result).Value!;
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal(new CarouselResponse(SiteId, 3, 6, body.Slides), body);
        Assert.Equal($"/carousels/images/{ImageId}", body.Slides[0].ImageUrl);
        Assert.Equal("#/about", body.Slides[0].LinkUrl);
    }

    [Fact]
    public async Task GetPublic_DropsUnsafeStoredLinks()
    {
        var queries = Substitute.For<ICarouselQueries>();
        queries.FindLive(SiteId, Arg.Any<CancellationToken>()).Returns(Some(Detail("javascript:alert(1)")));

        var result = await CarouselEndpoints.GetPublic(SiteId, new DefaultHttpContext(), queries);

        Assert.Equal(string.Empty, Assert.IsType<JsonHttpResult<CarouselResponse>>(result).Value!.Slides[0].LinkUrl);
    }

    [Fact]
    public async Task GetPublic_WithoutACarousel_ReturnsNotFound()
    {
        var queries = Substitute.For<ICarouselQueries>();
        queries.FindLive(SiteId, Arg.Any<CancellationToken>()).Returns(None);

        Assert.IsType<NotFound>(await CarouselEndpoints.GetPublic(SiteId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task GetManaged_ReadsTheActiveCarouselWithoutCaching()
    {
        var queries = Substitute.For<ICarouselQueries>();
        queries.FindManaged(SiteId, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(Some(Detail()));
        var context = new DefaultHttpContext();

        var result = await CarouselEndpoints.GetManaged(SiteId, context, queries, new FakeAuthorizationService());

        Assert.IsType<JsonHttpResult<CarouselResponse>>(result);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetManaged_DeletedWithoutTheDeletedRecordsGrant_IsForbidden()
    {
        var queries = Substitute.For<ICarouselQueries>();

        var result = await CarouselEndpoints.GetManaged(SiteId, new DefaultHttpContext(), queries, new FakeAuthorizationService(), true);

        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await queries.DidNotReceiveWithAnyArgs().FindManaged(default, default, default);
    }

    [Fact]
    public async Task GetManaged_DeletedWithTheGrant_ReadsTheDeletedCarousel()
    {
        var queries = Substitute.For<ICarouselQueries>();
        queries.FindManaged(SiteId, DeletionState.Deleted, Arg.Any<CancellationToken>()).Returns(Some(Detail()));

        var result = await CarouselEndpoints.GetManaged(
            SiteId,
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            true);

        Assert.IsType<JsonHttpResult<CarouselResponse>>(result);
    }

    [Fact]
    public async Task GetManaged_WithoutACarousel_ReturnsNotFound()
    {
        var queries = Substitute.For<ICarouselQueries>();
        queries.FindManaged(default, default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await CarouselEndpoints.GetManaged(SiteId, new DefaultHttpContext(), queries, new FakeAuthorizationService()));
    }

    [Fact]
    public async Task Save_PassesTheTrimmedDraftToTheCommands()
    {
        var commands = Substitute.For<ICarouselCommands>();
        commands.Save(default, default!, default).ReturnsForAnyArgs(new CarouselSaveResult.Saved(new VersionedKey<Guid>(SiteId, 2)));

        var result = await CarouselEndpoints.Save(
            SiteId,
            Request(8, new CarouselEndpoints.SaveCarouselSlideRequest(ImageId, "  Hello  ", " https://example.com ")),
            new DefaultHttpContext(),
            commands,
            Options());

        Assert.Equal(new CarouselSavedResponse(SiteId, 2), Assert.IsType<JsonHttpResult<CarouselSavedResponse>>(result).Value);
        await commands.Received(1).Save(
            SiteId,
            Arg.Is<ICarouselCommands.CarouselDraft>(draft =>
                draft.IntervalSeconds == 8
                && draft.Slides.Single().Caption == "Hello"
                && draft.Slides.Single().LinkUrl == "https://example.com"
                && draft.Slides.Single().ImageId == ImageId),
            Arg.Any<CancellationToken>());
    }

    public static TheoryData<CarouselEndpoints.SaveCarouselRequest> InvalidRequests =>
        new()
        {
            new(1, [new(ImageId, "x", null)]),
            new(61, [new(ImageId, "x", null)]),
            new(5, null),
            new(5, []),
            new(5, [new(Guid.Empty, "x", null)]),
            new(5, [new(ImageId, "x", "javascript:alert(1)")]),
            new(5, [new(ImageId, new string('x', CarouselsConfiguration.DefaultMaxCaptionLength + 1), null)]),
            new(5, Enumerable.Range(0, CarouselsConfiguration.DefaultMaxSlides + 1).Select(_ => new CarouselEndpoints.SaveCarouselSlideRequest(ImageId, null, null)).ToList())
        };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Save_InvalidRequest_ReturnsBadRequestWithoutSaving(CarouselEndpoints.SaveCarouselRequest request)
    {
        var commands = Substitute.For<ICarouselCommands>();

        Assert.IsType<BadRequest>(await CarouselEndpoints.Save(SiteId, request, new DefaultHttpContext(), commands, Options()));
        await commands.DidNotReceiveWithAnyArgs().Save(default, default!, default);
    }

    [Fact]
    public async Task Save_MapsTheCommandResults()
    {
        var commands = Substitute.For<ICarouselCommands>();

        commands.Save(default, default!, default).ReturnsForAnyArgs(new CarouselSaveResult.NotFound());
        Assert.IsType<NotFound>(await CarouselEndpoints.Save(SiteId, Request(), new DefaultHttpContext(), commands, Options()));

        commands.Save(default, default!, default).ReturnsForAnyArgs(new CarouselSaveResult.UnknownImage());
        Assert.IsType<BadRequest>(await CarouselEndpoints.Save(SiteId, Request(), new DefaultHttpContext(), commands, Options()));

        commands.Save(default, default!, default).ReturnsForAnyArgs(new CarouselSaveResult.VersionConflict());
        Assert.Equal(
            new CarouselConflictResponse("version-conflict"),
            Assert.IsType<Conflict<CarouselConflictResponse>>(await CarouselEndpoints.Save(SiteId, Request(), new DefaultHttpContext(), commands, Options())).Value);
    }

    [Fact]
    public async Task Save_EnforcesTheConfiguredLimitsInsteadOfTheDefaults()
    {
        var commands = Substitute.For<ICarouselCommands>();
        commands.Save(default, default!, default).ReturnsForAnyArgs(new CarouselSaveResult.Saved(new VersionedKey<Guid>(SiteId, 1)));
        var strict = Options(new CarouselsConfiguration
        {
            MaxSlides = 1,
            MinIntervalSeconds = 10,
            MaxIntervalSeconds = 20,
            MaxCaptionLength = 5
        });
        var slide = new CarouselEndpoints.SaveCarouselSlideRequest(ImageId, "short", null);

        Assert.IsType<BadRequest>(await CarouselEndpoints.Save(SiteId, Request(5, slide), new DefaultHttpContext(), commands, strict));
        Assert.IsType<BadRequest>(await CarouselEndpoints.Save(SiteId, Request(21, slide), new DefaultHttpContext(), commands, strict));
        Assert.IsType<BadRequest>(await CarouselEndpoints.Save(SiteId, Request(10, slide, slide), new DefaultHttpContext(), commands, strict));
        Assert.IsType<BadRequest>(await CarouselEndpoints.Save(
            SiteId,
            Request(10, new CarouselEndpoints.SaveCarouselSlideRequest(ImageId, "too long", null)),
            new DefaultHttpContext(),
            commands,
            strict));
        Assert.IsType<JsonHttpResult<CarouselSavedResponse>>(
            await CarouselEndpoints.Save(SiteId, Request(10, slide), new DefaultHttpContext(), commands, strict));
    }

    [Fact]
    public async Task Save_AcceptsMoreSlidesAndLongerIntervalsWhenTheConfigurationAllowsThem()
    {
        var commands = Substitute.For<ICarouselCommands>();
        commands.Save(default, default!, default).ReturnsForAnyArgs(new CarouselSaveResult.Saved(new VersionedKey<Guid>(SiteId, 1)));
        var relaxed = Options(new CarouselsConfiguration { MaxSlides = 30, MaxIntervalSeconds = 600 });
        var slides = Enumerable.Range(0, 30)
            .Select(_ => new CarouselEndpoints.SaveCarouselSlideRequest(ImageId, null, null))
            .ToArray();

        Assert.IsType<JsonHttpResult<CarouselSavedResponse>>(
            await CarouselEndpoints.Save(SiteId, Request(600, slides), new DefaultHttpContext(), commands, relaxed));
    }

    [Fact]
    public async Task UploadCarousel_UsesTheConfiguredImageLimit()
    {
        IFormFile file = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "slide.png");

        var result = await SiteImageEndpoints.UploadCarousel(
            file,
            new DefaultHttpContext(),
            Substitute.For<IVfsCommands>(),
            Options(new CarouselsConfiguration { MaxImageBytes = 4 }));

        var response = Assert.IsType<JsonHttpResult<SiteImageLimitResponse>>(result);
        Assert.Equal(413, response.StatusCode);
        Assert.Equal(new SiteImageLimitResponse(4), response.Value);
    }

    [Fact]
    public async Task Delete_Restore_AndHardDelete_MapTheCommandResults()
    {
        var commands = Substitute.For<ICarouselCommands>();
        commands.Delete(SiteId, Arg.Any<CancellationToken>()).Returns(true, false);
        commands.Restore(SiteId, Arg.Any<CancellationToken>()).Returns(true, false);
        commands.HardDelete(SiteId, Arg.Any<CancellationToken>())
            .Returns(HardDeleteResult.Deleted, HardDeleteResult.NotFound, HardDeleteResult.Rejected);

        Assert.IsType<NoContent>(await CarouselEndpoints.Delete(SiteId, new DefaultHttpContext(), commands));
        Assert.IsType<NotFound>(await CarouselEndpoints.Delete(SiteId, new DefaultHttpContext(), commands));
        Assert.IsType<NoContent>(await CarouselEndpoints.Restore(SiteId, new DefaultHttpContext(), commands));
        Assert.IsType<NotFound>(await CarouselEndpoints.Restore(SiteId, new DefaultHttpContext(), commands));
        Assert.IsType<NoContent>(await CarouselEndpoints.HardDelete(SiteId, new DefaultHttpContext(), commands));
        Assert.IsType<NotFound>(await CarouselEndpoints.HardDelete(SiteId, new DefaultHttpContext(), commands));
        Assert.IsType<Conflict>(await CarouselEndpoints.HardDelete(SiteId, new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task UploadCarousel_StoresTheImageUnderTheCarouselKindAndReturnsItsOwnUrl()
    {
        var id = Guid.NewGuid();
        var commands = Substitute.For<IVfsCommands>();
        commands.Add(Arg.Any<IVfsCommands.Upload>(), Arg.Any<CancellationToken>()).Returns(id);
        IFormFile file = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "slide.png");

        var result = await SiteImageEndpoints.UploadCarousel(
            file,
            new DefaultHttpContext(),
            commands,
            Options());

        Assert.Equal(
            new SiteImageResponse(id, $"/carousels/images/{id}"),
            Assert.IsType<JsonHttpResult<SiteImageResponse>>(result).Value);
        await commands.Received(1).Add(
            Arg.Is<IVfsCommands.Upload>(upload => upload.Type == VfsTypes.Carousels && upload.ContentType == "image/png"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCarousel_ServesOnlyCarouselImages()
    {
        var id = Guid.NewGuid();
        var queries = Substitute.For<IVfsQueries>();
        queries.Open(VfsTypes.CarouselImages, id, Arg.Any<CancellationToken>())
            .Returns(Some(new IVfsQueries.Content(new VfsFile(VfsTypes.Carousels, "a.png", "image/png", 3), Png)));
        queries.Open(Arg.Is<IReadOnlyList<string>>(types => !types.SequenceEqual(VfsTypes.CarouselImages)), id, Arg.Any<CancellationToken>())
            .Returns(None);

        Assert.IsType<FileContentHttpResult>(await SiteImageEndpoints.GetCarousel(id, new DefaultHttpContext(), queries));
        Assert.IsType<NotFound>(await SiteImageEndpoints.Get(id, new DefaultHttpContext(), queries));
    }
}
