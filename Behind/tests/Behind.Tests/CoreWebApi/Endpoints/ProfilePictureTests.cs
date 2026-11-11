using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class ProfilePictureTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private ModuleFiles Files(AbstractCoreContext context) =>
        new(context, new VfsCommands(context, _bed.ContentStore), new VfsQueries(context, _bed.ContentStore));

    [Fact]
    public async Task Picture_IsStoredInUserVfsAttachedToOwnerAndServedOnlyWhileCurrent()
    {
        var bytes = TestImages.Png(16, 16);
        Guid picture;
        await using (var context = _bed.Write())
        {
            var files = Files(context);
            picture = await files.Add(new IModuleFiles.Upload(UserProfiles.FileType, "avatar.png", "image/png", bytes));
            var profiles = new UserProfiles(context, files);
            Assert.True(await profiles.SetPicture(_bed.AuthorId, picture, default));
        }

        await using (var context = _bed.Read())
        {
            var files = Files(context);
            var profiles = new UserProfiles(context, files);
            var profile = Assert.Single(await profiles.Find([_bed.AuthorId]));
            Assert.Equal(picture, profile.PictureId);
            var image = await files.Open(UserProfiles.FileType, picture);
            Assert.Equal(bytes, image.Match(() => Array.Empty<byte>(), content => content.Bytes));
            Assert.False((await _bed.ContentStore.Read(VfsTestPaths.Of(UserProfiles.FileType, picture, _bed.AuthorId))).IsNone());
            Assert.True((await _bed.ContentStore.Read(VfsTestPaths.Of(UserProfiles.FileType, picture))).IsNone());
            var served = Assert.IsType<FileContentHttpResult>(
                await ProfilePictureEndpoints.Image(picture, new DefaultHttpContext(), files, profiles));
            Assert.Equal(bytes, served.FileContents.ToArray());
            Assert.Equal("image/png", served.ContentType);
        }

        await using (var context = _bed.Write())
        {
            Assert.True(await new UserProfiles(context, Files(context)).SetPicture(_bed.AuthorId, null, default));
        }

        await using (var context = _bed.Read())
        {
            Assert.IsType<NotFound>(await ProfilePictureEndpoints.Image(picture, new DefaultHttpContext(),
                Files(context), new UserProfiles(context, Files(context))));
        }
    }

    [Fact]
    public async Task Upload_ValidatesImageTypeAndSizeAndRequiresIdentity()
    {
        await using var context = _bed.Write();
        var files = Files(context);
        var profiles = new UserProfiles(context, files);
        var identity = Substitute.For<IModuleIdentity>();
        identity.UserId(Arg.Any<HttpContext>()).Returns((Guid?)_bed.AuthorId);

        using var invalid = new MemoryStream([1, 2, 3]);
        var unsupported = await ProfilePictureEndpoints.Upload(
            new FormFile(invalid, 0, invalid.Length, "file", "not-image.png"),
            new DefaultHttpContext(), identity, files, profiles);
        Assert.Equal(415, Assert.IsAssignableFrom<IStatusCodeHttpResult>(unsupported).StatusCode);

        var tooLarge = await ProfilePictureEndpoints.Upload(
            new FormFile(Stream.Null, 0, ProfilePictureEndpoints.MaxBytes + 1, "file", "large.png"),
            new DefaultHttpContext(), identity, files, profiles);
        Assert.Equal(413, Assert.IsAssignableFrom<IStatusCodeHttpResult>(tooLarge).StatusCode);

        identity.UserId(Arg.Any<HttpContext>()).Returns((Guid?)null);
        Assert.IsType<UnauthorizedHttpResult>(await ProfilePictureEndpoints.Upload(
            null, new DefaultHttpContext(), identity, files, profiles));
        Assert.Empty(await context.VfsFiles.ToListAsync());
    }

    [Fact]
    public async Task Upload_UsesDetectedMimeAndReturnsProfileUrl()
    {
        await using var context = _bed.Write();
        var files = Files(context);
        var profiles = new UserProfiles(context, files);
        var identity = Substitute.For<IModuleIdentity>();
        identity.UserId(Arg.Any<HttpContext>()).Returns((Guid?)_bed.AuthorId);
        using var stream = new MemoryStream(TestImages.Png(16, 16));
        var result = await ProfilePictureEndpoints.Upload(
            new FormFile(stream, 0, stream.Length, "file", "avatar.png") { Headers = new HeaderDictionary(), ContentType = "text/plain" },
            new DefaultHttpContext(), identity, files, profiles);
        var response = Assert.IsType<JsonHttpResult<ImageUploadResponse>>(result).Value!;
        Assert.Equal($"/profile/images/{response.Id}", response.Url);
        Assert.Equal(response.Id, Assert.Single(await profiles.Find([_bed.AuthorId])).PictureId);
        Assert.Equal("image/png", (await files.Open(UserProfiles.FileType, response.Id))
            .Match(() => "", file => file.ContentType));
    }
}
