using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class GalleryImageEndpointsTests
{
    private static IOptionsSnapshot<GalleriesConfiguration> Options(long? maxBytes = null) =>
        FixedOptionsSnapshot.Of(
            maxBytes is { } limit ? new GalleriesConfiguration { MaxImageBytes = limit } : new GalleriesConfiguration());

    private static IFormFile File(byte[] content, string fileName = "photo.png") =>
        new FormFile(new MemoryStream(content), 0, content.Length, "files", fileName);

    private static FormFileCollection Files(params IFormFile[] files)
    {
        var collection = new FormFileCollection();
        collection.AddRange(files);

        return collection;
    }

    private static IVfsCommands Commands(List<IVfsCommands.Upload> stored)
    {
        var commands = Substitute.For<IVfsCommands>();
        commands.Add(Arg.Any<IVfsCommands.Upload>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            stored.Add(call.Arg<IVfsCommands.Upload>());

            return Guid.NewGuid();
        });

        return commands;
    }

    private static Task<IResult> Upload(
        FormFileCollection? files,
        IVfsCommands commands,
        IOptionsSnapshot<GalleriesConfiguration>? options = null) =>
        GalleryImageEndpoints.UploadImages(files, new DefaultHttpContext(), commands, options ?? Options());

    private static Task<IResult> UploadCover(
        IFormFile? file,
        IVfsCommands commands,
        IOptionsSnapshot<GalleriesConfiguration>? options = null) =>
        GalleryImageEndpoints.UploadThumbnail(file, new DefaultHttpContext(), commands, options ?? Options());

    [Fact]
    public async Task UploadImages_StoresTheOriginalAndAThumbnailOfEveryFileInTheGalleryImageStore()
    {
        var stored = new List<IVfsCommands.Upload>();
        var original = TestImages.Png(800, 600);

        var result = await Upload(
            Files(File(original, "C:\\pics\\one.png"), File(TestImages.Jpeg(100, 100), "two.jpg")),
            Commands(stored));

        var body = Assert.IsType<JsonHttpResult<GalleryUploadResponse>>(result).Value!;
        Assert.Equal(["one.png", "two.jpg"], body.Images.Select(image => image.FileName));
        Assert.All(body.Images, image =>
        {
            Assert.Null(image.Error);
            Assert.Equal($"/galleries/images/{image.ImageId}", image.ImageUrl);
            Assert.Equal($"/galleries/images/{image.ThumbnailId}", image.ThumbnailUrl);
            Assert.NotEqual(image.ImageId, image.ThumbnailId);
        });
        Assert.Equal(4, stored.Count);
        Assert.All(stored, upload => Assert.Equal(VfsTypes.Galleries, upload.Type));
        Assert.Equal("image/png", stored[0].ContentType);
        Assert.Equal(original, stored[0].Content);
        Assert.Equal("image/webp", stored[1].ContentType);
        Assert.Equal((400, 300), TestImages.SizeOf(stored[1].Content));
        Assert.Equal("image/jpeg", stored[2].ContentType);
    }

    [Fact]
    public async Task UploadImages_RejectsOnlyTheFilesThatCannotBeUsed()
    {
        var stored = new List<IVfsCommands.Upload>();
        var tooLarge = TestImages.Png(300, 300);
        var files = Files(
            File("<svg onload=alert(1)/>"u8.ToArray(), "x.svg"),
            File([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3], "broken.png"),
            File(tooLarge, "big.png"),
            File(TestImages.Png(10, 10), "ok.png"));

        var result = await Upload(files, Commands(stored), Options(tooLarge.Length - 1));

        var body = Assert.IsType<JsonHttpResult<GalleryUploadResponse>>(result).Value!;
        Assert.Equal(
            [("x.svg", "unsupported"), ("broken.png", "unreadable"), ("big.png", "too-large"), ("ok.png", null)],
            body.Images.Select(image => (image.FileName, image.Error)));
        Assert.Equal(["ok.png", "ok.png"], stored.Select(image => image.FileName));
        Assert.All(body.Images.Take(3), image => Assert.Null(image.ImageId));
    }

    [Fact]
    public async Task UploadImages_AcceptsAFileExactlyAtTheGalleryImageLimit()
    {
        var content = TestImages.Png(300, 300);
        var result = await Upload(Files(File(content)), Commands([]), Options(content.Length));

        Assert.Null(Assert.Single(Assert.IsType<JsonHttpResult<GalleryUploadResponse>>(result).Value!.Images).Error);
    }

    [Fact]
    public async Task UploadImages_AnEmptyFile_IsUnreadable()
    {
        var result = await Upload(Files(File([], "empty.png")), Commands([]));

        var image = Assert.Single(Assert.IsType<JsonHttpResult<GalleryUploadResponse>>(result).Value!.Images);
        Assert.Equal("unreadable", image.Error);
    }

    [Fact]
    public async Task UploadImages_NoFilesOrTooManyFiles_ReturnsBadRequest()
    {
        var commands = Commands([]);
        var tooMany = Files(Enumerable.Range(0, GalleryImageEndpoints.MaxUploadFiles + 1)
            .Select(_ => File(TestImages.Png(5, 5)))
            .ToArray());

        Assert.IsType<BadRequest>(await Upload(null, commands));
        Assert.IsType<BadRequest>(await Upload(Files(), commands));
        Assert.IsType<BadRequest>(await Upload(tooMany, commands));
        await commands.DidNotReceiveWithAnyArgs().Add(default!, default);
    }

    [Fact]
    public async Task UploadThumbnail_StoresAScaledCover()
    {
        var stored = new List<IVfsCommands.Upload>();
        var id = Guid.NewGuid();
        var commands = Substitute.For<IVfsCommands>();
        commands.Add(Arg.Any<IVfsCommands.Upload>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            stored.Add(call.Arg<IVfsCommands.Upload>());

            return id;
        });

        var result = await UploadCover(File(TestImages.Png(1200, 600), "cover.png"), commands);

        Assert.Equal(
            new GalleryImageResponse(id, $"/galleries/images/{id}"),
            Assert.IsType<JsonHttpResult<GalleryImageResponse>>(result).Value);
        var image = Assert.Single(stored);
        Assert.Equal((VfsTypes.Galleries, "cover.png", "image/webp"), (image.Type, image.FileName, image.ContentType));
        Assert.Equal((GalleryImageEndpoints.CoverSide, GalleryImageEndpoints.CoverSide / 2), TestImages.SizeOf(image.Content));
    }

    [Fact]
    public async Task UploadThumbnail_RejectsMissingOversizedAndUnusableFiles()
    {
        var commands = Commands([]);
        var big = TestImages.Png(300, 300);

        Assert.IsType<BadRequest>(await UploadCover(null, commands));
        Assert.IsType<BadRequest>(await UploadCover(File([]), commands));

        var large = await UploadCover(File(big), commands, Options(big.Length - 1));
        var response = Assert.IsType<JsonHttpResult<GalleryImageLimitResponse>>(large);
        Assert.Equal((StatusCodes.Status413PayloadTooLarge, big.Length - 1L), (response.StatusCode, response.Value!.MaxBytes));

        var notAnImage = await UploadCover(File("hello"u8.ToArray()), commands);
        var broken = await UploadCover(File([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1]), commands);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeHttpResult>(notAnImage).StatusCode);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeHttpResult>(broken).StatusCode);
        await commands.DidNotReceiveWithAnyArgs().Add(default!, default);
    }

    [Fact]
    public async Task Get_ServesTheImageWithImmutableCachingAndNoSniffing()
    {
        var id = Guid.NewGuid();
        var queries = Substitute.For<IVfsQueries>();
        queries.Open(VfsTypes.Galleries, id, Arg.Any<CancellationToken>())
            .Returns(new IVfsQueries.Content(new VfsFile(VfsTypes.Galleries, "a.webp", "image/webp", 3), [1, 2, 3]));
        var context = new DefaultHttpContext();

        var result = await GalleryImageEndpoints.Get(id, context, queries);

        var file = Assert.IsType<FileContentHttpResult>(result);
        Assert.Equal("image/webp", file.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.FileContents.ToArray());
        Assert.Equal("public, max-age=31536000, immutable", context.Response.Headers.CacheControl);
        Assert.Equal("nosniff", context.Response.Headers[HeaderNames.XContentTypeOptions]);
    }

    [Fact]
    public async Task Get_UnknownImage_ReturnsNotFound()
    {
        var queries = Substitute.For<IVfsQueries>();
        queries.Open(default!, default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await GalleryImageEndpoints.Get(Guid.NewGuid(), new DefaultHttpContext(), queries));
    }
}
