using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Site = Behind.Core.Domain.Site;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SiteImageEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static IOptionsSnapshot<SitesConfiguration> Options(long? maxBytes = null) =>
        FixedOptionsSnapshot.Of(
            maxBytes is { } limit ? new SitesConfiguration { MaxImageBytes = limit } : new SitesConfiguration());

    private static IFormFile File(byte[] content, string fileName = "logo.png") =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);

    [Fact]
    public async Task Upload_StoresTheImageAndReturnsItsUrl()
    {
        var id = Guid.NewGuid();
        var commands = Substitute.For<IVfsCommands>();
        commands.Add(Arg.Any<IVfsCommands.Upload>(), Arg.Any<CancellationToken>()).Returns(id);

        var result = await SiteImageEndpoints.Upload(
            File(Png, "C:\\pics\\logo.png"),
            new DefaultHttpContext(),
            commands,
            Options());

        var body = Assert.IsType<JsonHttpResult<SiteImageResponse>>(result).Value!;
        Assert.Equal(new SiteImageResponse(id, $"/sites/images/{id}"), body);
        await commands.Received(1).Add(
            Arg.Is<IVfsCommands.Upload>(upload =>
                upload.Type == VfsTypes.Sites
                && upload.FileName == "logo.png"
                && upload.ContentType == "image/png"
                && upload.Content.SequenceEqual(Png)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadNews_AndUploadArticle_StoreTheImageUnderTheirOwnType()
    {
        var commands = Substitute.For<IVfsCommands>();
        commands.Add(Arg.Any<IVfsCommands.Upload>(), Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        await SiteImageEndpoints.UploadNews(File(Png), new DefaultHttpContext(), commands, Options());
        await SiteImageEndpoints.UploadArticle(File(Png), new DefaultHttpContext(), commands, Options());

        await commands.Received(1).Add(
            Arg.Is<IVfsCommands.Upload>(upload => upload.Type == VfsTypes.News),
            Arg.Any<CancellationToken>());
        await commands.Received(1).Add(
            Arg.Is<IVfsCommands.Upload>(upload => upload.Type == VfsTypes.Articles),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_TrustsTheContentNotTheDeclaredType()
    {
        var commands = Substitute.For<IVfsCommands>();
        var file = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "x.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/html"
        };

        await SiteImageEndpoints.Upload(file, new DefaultHttpContext(), commands, Options());

        await commands.Received(1).Add(Arg.Is<IVfsCommands.Upload>(upload => upload.ContentType == "image/png"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_NoFile_ReturnsBadRequest()
    {
        var commands = Substitute.For<IVfsCommands>();

        Assert.IsType<BadRequest>(await SiteImageEndpoints.Upload(null, new DefaultHttpContext(), commands, Options()));
        Assert.IsType<BadRequest>(await SiteImageEndpoints.Upload(File([]), new DefaultHttpContext(), commands, Options()));
    }

    [Fact]
    public async Task Upload_NotAnImage_ReturnsUnsupportedMediaType()
    {
        var commands = Substitute.For<IVfsCommands>();

        var result = await SiteImageEndpoints.Upload(
            File("<svg onload=alert(1)/>"u8.ToArray(), "x.svg"),
            new DefaultHttpContext(),
            commands,
            Options());

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await commands.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Upload_TooLarge_ReturnsPayloadTooLarge()
    {
        var commands = Substitute.For<IVfsCommands>();
        var large = new byte[SitesConfiguration.DefaultMaxImageBytes + 1];

        var result = await SiteImageEndpoints.Upload(File(large), new DefaultHttpContext(), commands, Options());

        var body = Assert.IsType<JsonHttpResult<SiteImageLimitResponse>>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, body.StatusCode);
        Assert.Equal(new SiteImageLimitResponse(SitesConfiguration.DefaultMaxImageBytes), body.Value);
        await commands.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Upload_UsesTheConfiguredLimit()
    {
        var commands = Substitute.For<IVfsCommands>();
        commands.Add(Arg.Any<IVfsCommands.Upload>(), Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var rejected = await SiteImageEndpoints.Upload(File(Png), new DefaultHttpContext(), commands, Options(Png.Length - 1));
        var accepted = await SiteImageEndpoints.Upload(File(Png), new DefaultHttpContext(), commands, Options(Png.Length));

        Assert.Equal(new SiteImageLimitResponse(Png.Length - 1), Assert.IsType<JsonHttpResult<SiteImageLimitResponse>>(rejected).Value);
        Assert.IsType<JsonHttpResult<SiteImageResponse>>(accepted);
    }

    [Fact]
    public async Task Get_ServesTheImageForeverWithoutSniffing()
    {
        var id = Guid.NewGuid();
        var queries = Substitute.For<IVfsQueries>();
        queries.Open(VfsTypes.Sites, id, Arg.Any<CancellationToken>())
            .Returns(Some(new IVfsQueries.Content(new VfsFile(VfsTypes.Sites, "logo.png", "image/png", Png.Length), Png)));
        var context = new DefaultHttpContext();

        var result = await SiteImageEndpoints.Get(id, context, queries);

        var file = Assert.IsType<FileContentHttpResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(Png, file.FileContents.ToArray());
        Assert.Equal(SiteImageEndpoints.ImmutableCache, context.Response.Headers.CacheControl);
        Assert.Equal("nosniff", context.Response.Headers[HeaderNames.XContentTypeOptions]);
    }

    [Fact]
    public async Task Get_UnknownImage_ReturnsNotFound()
    {
        var queries = Substitute.For<IVfsQueries>();
        queries.Open(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(None);

        Assert.IsType<NotFound>(await SiteImageEndpoints.Get(Guid.NewGuid(), new DefaultHttpContext(), queries));
    }
}
