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

    private static IOptions<SitesConfiguration> Options(long? maxBytes = null) =>
        Microsoft.Extensions.Options.Options.Create(
            maxBytes is { } limit ? new SitesConfiguration { MaxImageBytes = limit } : new SitesConfiguration());

    private static IFormFile File(byte[] content, string fileName = "logo.png") =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);

    [Fact]
    public async Task Upload_StoresTheImageAndReturnsItsUrl()
    {
        var id = Guid.NewGuid();
        var commands = Substitute.For<ISiteImageCommands>();
        commands.Add(Arg.Any<SiteImage>(), Arg.Any<CancellationToken>()).Returns(id);

        var result = await SiteImageEndpoints.Upload(
            File(Png, "C:\\pics\\logo.png"),
            new DefaultHttpContext(),
            commands,
            new FixedTimeProvider(Now), Options());

        var body = Assert.IsType<JsonHttpResult<SiteImageResponse>>(result).Value!;
        Assert.Equal(new SiteImageResponse(id, $"/sites/images/{id}"), body);
        await commands.Received(1).Add(
            Arg.Is<SiteImage>(image =>
                image.FileName == "logo.png"
                && image.ContentType == "image/png"
                && image.Content.SequenceEqual(Png)
                && image.CreatedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_TrustsTheContentNotTheDeclaredType()
    {
        var commands = Substitute.For<ISiteImageCommands>();
        var file = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "x.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/html"
        };

        await SiteImageEndpoints.Upload(file, new DefaultHttpContext(), commands, new FixedTimeProvider(Now), Options());

        await commands.Received(1).Add(Arg.Is<SiteImage>(image => image.ContentType == "image/png"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_NoFile_ReturnsBadRequest()
    {
        var commands = Substitute.For<ISiteImageCommands>();

        Assert.IsType<BadRequest>(await SiteImageEndpoints.Upload(null, new DefaultHttpContext(), commands, new FixedTimeProvider(Now), Options()));
        Assert.IsType<BadRequest>(await SiteImageEndpoints.Upload(File([]), new DefaultHttpContext(), commands, new FixedTimeProvider(Now), Options()));
    }

    [Fact]
    public async Task Upload_NotAnImage_ReturnsUnsupportedMediaType()
    {
        var commands = Substitute.For<ISiteImageCommands>();

        var result = await SiteImageEndpoints.Upload(
            File("<svg onload=alert(1)/>"u8.ToArray(), "x.svg"),
            new DefaultHttpContext(),
            commands,
            new FixedTimeProvider(Now), Options());

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await commands.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Upload_TooLarge_ReturnsPayloadTooLarge()
    {
        var commands = Substitute.For<ISiteImageCommands>();
        var large = new byte[SitesConfiguration.DefaultMaxImageBytes + 1];

        var result = await SiteImageEndpoints.Upload(File(large), new DefaultHttpContext(), commands, new FixedTimeProvider(Now), Options());

        var body = Assert.IsType<JsonHttpResult<SiteImageLimitResponse>>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, body.StatusCode);
        Assert.Equal(new SiteImageLimitResponse(SitesConfiguration.DefaultMaxImageBytes), body.Value);
        await commands.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Upload_UsesTheConfiguredLimit()
    {
        var commands = Substitute.For<ISiteImageCommands>();
        commands.Add(Arg.Any<SiteImage>(), Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var rejected = await SiteImageEndpoints.Upload(File(Png), new DefaultHttpContext(), commands, new FixedTimeProvider(Now), Options(Png.Length - 1));
        var accepted = await SiteImageEndpoints.Upload(File(Png), new DefaultHttpContext(), commands, new FixedTimeProvider(Now), Options(Png.Length));

        Assert.Equal(new SiteImageLimitResponse(Png.Length - 1), Assert.IsType<JsonHttpResult<SiteImageLimitResponse>>(rejected).Value);
        Assert.IsType<JsonHttpResult<SiteImageResponse>>(accepted);
    }

    [Fact]
    public async Task Get_ServesTheImageForeverWithoutSniffing()
    {
        var id = Guid.NewGuid();
        var queries = Substitute.For<ISiteImageQueries>();
        queries.Find(id, Arg.Any<CancellationToken>()).Returns(Some(new SiteImage("logo.png", "image/png", Png, Now)));
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
        var queries = Substitute.For<ISiteImageQueries>();
        queries.Find(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(None);

        Assert.IsType<NotFound>(await SiteImageEndpoints.Get(Guid.NewGuid(), new DefaultHttpContext(), queries));
    }
}
