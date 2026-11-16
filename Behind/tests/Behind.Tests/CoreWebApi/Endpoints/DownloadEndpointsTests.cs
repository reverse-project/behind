using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class DownloadEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid FolderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DownloadId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static IOptionsSnapshot<DownloadsConfiguration> Options(long? maxBytes = null) =>
        FixedOptionsSnapshot.Of(
            maxBytes is { } limit ? new DownloadsConfiguration { MaxFileBytes = limit } : new DownloadsConfiguration());

    private static IDownloadQueries.DownloadInfo Info(string name = "Guide") =>
        new(DownloadId, FolderId, name, "guide.pdf", "application/pdf", 2048, "Ada", Some(Now));

    private static IFormFile File(byte[] content, string fileName = "guide.pdf", string contentType = "application/pdf") =>
        new FormFile(new MemoryStream(content), 0, content.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static FormCollection Form(IEnumerable<IFormFile> files, params string[] displayNames)
    {
        var collection = new FormFileCollection();
        collection.AddRange(files.ToArray());

        return new FormCollection(
            displayNames.Length > 0
                ? new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    [DownloadEndpoints.DisplayNamesField] = displayNames
                }
                : [],
            collection);
    }

    private static IDownloadCommands AddingCommands(
        List<(Guid Folder, Guid User, IDownloadCommands.FileDraft Draft)> added,
        Func<DownloadSaveResult>? outcome = null)
    {
        var commands = Substitute.For<IDownloadCommands>();
        commands.Add(default, default, default!, default).ReturnsForAnyArgs(call =>
        {
            added.Add((call.ArgAt<Guid>(0), call.ArgAt<Guid>(1), call.ArgAt<IDownloadCommands.FileDraft>(2)));

            return outcome?.Invoke() ?? new DownloadSaveResult.Saved(new VersionedKey<Guid>(Guid.NewGuid(), 1));
        });

        return commands;
    }

    private static Task<IResult> UploadAs(
        FormCollection? form,
        IDownloadCommands commands,
        bool valid = true,
        IOptionsSnapshot<DownloadsConfiguration>? options = null) =>
        DownloadEndpoints.Upload(
            FolderId,
            form,
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId, valid),
            commands,
            options ?? Options());

    private static void AssertOutcome(IResult result, int status, string? reason = null)
    {
        switch (result)
        {
            case IStatusCodeHttpResult { StatusCode: { } code } when code == status:
                break;
            case JsonHttpResult<NewsSavedResponse> when status == 200:
                break;
            default:
                Assert.Fail($"Expected {status} but got {result.GetType().Name}");
                break;
        }

        if (reason is not null)
        {
            Assert.Equal(reason, Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
        }
    }

    [Fact]
    public async Task ListFolders_ReturnsTheFoldersAndTheConfiguredLimitWithRevalidation()
    {
        var parent = Guid.NewGuid();
        var queries = Substitute.For<IDownloadQueries>();
        queries.ListFolders(Arg.Any<CancellationToken>()).Returns(
        [
            new IDownloadQueries.DownloadFolderInfo(FolderId, None, "manuals", "Manuals"),
            new IDownloadQueries.DownloadFolderInfo(parent, Some(FolderId), "drivers", "Drivers")
        ]);
        var context = new DefaultHttpContext();

        var result = await DownloadEndpoints.ListFolders(context, queries, Options(4096));

        var body = Assert.IsType<JsonHttpResult<DownloadFoldersResponse>>(result).Value!;
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal(4096, body.MaxFileBytes);
        Assert.Equal(
            [
                new DownloadFolderItem(FolderId, null, "manuals", "Manuals"),
                new DownloadFolderItem(parent, FolderId, "drivers", "Drivers")
            ],
            body.Folders);
    }

    [Fact]
    public async Task ListFolders_WithoutAConfiguredLimit_ReportsTheDefault()
    {
        var queries = Substitute.For<IDownloadQueries>();
        queries.ListFolders(Arg.Any<CancellationToken>()).Returns([]);

        var result = await DownloadEndpoints.ListFolders(new DefaultHttpContext(), queries, Options());

        Assert.Equal(
            DownloadsConfiguration.DefaultMaxFileBytes,
            Assert.IsType<JsonHttpResult<DownloadFoldersResponse>>(result).Value!.MaxFileBytes);
    }

    [Fact]
    public async Task ListFiles_ReturnsTheFilesWithTheirDownloadUrl()
    {
        var queries = Substitute.For<IDownloadQueries>();
        queries.ListFiles(FolderId, Arg.Any<CancellationToken>())
            .Returns(Some<IReadOnlyList<IDownloadQueries.DownloadInfo>>([Info()]));

        var result = await DownloadEndpoints.ListFiles(FolderId, new DefaultHttpContext(), queries);

        Assert.Equal(
            new DownloadFileItem(
                DownloadId,
                FolderId,
                "Guide",
                "guide.pdf",
                "application/pdf",
                2048,
                "Ada",
                Now,
                $"/downloads/files/{DownloadId}"),
            Assert.Single(Assert.IsType<JsonHttpResult<DownloadFilesResponse>>(result).Value!.Files));
    }

    [Fact]
    public async Task ListFiles_UnknownFolder_IsNotFound()
    {
        var queries = Substitute.For<IDownloadQueries>();
        queries.ListFiles(default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await DownloadEndpoints.ListFiles(FolderId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task ListDeleted_ReturnsTheDeletedFoldersAndFilesWithoutCaching()
    {
        var queries = Substitute.For<IDownloadQueries>();
        queries.ListDeleted(Arg.Any<CancellationToken>()).Returns(new IDownloadQueries.DeletedDownloads(
            [new IDownloadQueries.DownloadFolderInfo(FolderId, None, "gone", "Gone")],
            [Info("Old")]));
        var context = new DefaultHttpContext();

        var result = await DownloadEndpoints.ListDeleted(context, queries);

        var body = Assert.IsType<JsonHttpResult<DownloadDeletedResponse>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("Gone", Assert.Single(body.Folders).DisplayName);
        Assert.Equal("Old", Assert.Single(body.Files).DisplayName);
    }

    [Fact]
    public async Task Get_SendsTheFileAsAnAttachmentWithItsOriginalNameAndNoSniffing()
    {
        var file = new VfsFile(
            new VersionedKey<Guid>(Guid.NewGuid(), 2),
            VfsTypes.Downloads,
            "guide.pdf",
            "application/pdf",
            3,
            DownloadId);
        var queries = Substitute.For<IDownloadQueries>();
        queries.Open(DownloadId, Arg.Any<CancellationToken>())
            .Returns(Some(new IVfsQueries.Content(file, [1, 2, 3])));
        var context = new DefaultHttpContext();

        var result = await DownloadEndpoints.Get(DownloadId, context, queries);

        var body = Assert.IsType<FileContentHttpResult>(result);
        Assert.Equal("application/pdf", body.ContentType);
        Assert.Equal("guide.pdf", body.FileDownloadName);
        Assert.Equal([1, 2, 3], body.FileContents.ToArray());
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal("nosniff", context.Response.Headers[HeaderNames.XContentTypeOptions]);
    }

    [Fact]
    public async Task Get_UnknownDownload_IsNotFound()
    {
        var queries = Substitute.For<IDownloadQueries>();
        queries.Open(default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await DownloadEndpoints.Get(DownloadId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task CreateFolder_StoresARootFolderWithTheSlugOfTheName()
    {
        var commands = Substitute.For<IDownloadFolderCommands>();
        commands.Create(default, default!, default).ReturnsForAnyArgs(
            new DownloadFolderSaveResult.Saved(new VersionedKey<Guid>(FolderId, 1)));

        var result = await DownloadEndpoints.CreateFolder(
            new DownloadEndpoints.SaveFolderRequest("  User Manuals ", null),
            new DefaultHttpContext(),
            commands);

        AssertOutcome(result, 200);
        await commands.Received(1).Create(
            None,
            new IDownloadFolderCommands.FolderDraft("user-manuals", "User Manuals"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSubfolder_StoresTheFolderInsideTheParentWithTheGivenSlug()
    {
        var commands = Substitute.For<IDownloadFolderCommands>();
        commands.Create(default, default!, default).ReturnsForAnyArgs(
            new DownloadFolderSaveResult.Saved(new VersionedKey<Guid>(DownloadId, 1)));

        var result = await DownloadEndpoints.CreateSubfolder(
            FolderId,
            new DownloadEndpoints.SaveFolderRequest("Drivers", "Windows Drivers!"),
            new DefaultHttpContext(),
            commands);

        AssertOutcome(result, 200);
        await commands.Received(1).Create(
            Some(FolderId),
            new IDownloadFolderCommands.FolderDraft("windows-drivers", "Drivers"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Manuals", "!!!")]
    [InlineData("!!!", null)]
    public async Task SaveFolder_WithoutAUsableNameOrSlug_IsABadRequest(string displayName, string? slug)
    {
        var commands = Substitute.For<IDownloadFolderCommands>();
        var request = new DownloadEndpoints.SaveFolderRequest(displayName, slug);

        Assert.IsType<BadRequest>(await DownloadEndpoints.CreateFolder(request, new DefaultHttpContext(), commands));
        Assert.IsType<BadRequest>(await DownloadEndpoints.UpdateFolder(FolderId, request, new DefaultHttpContext(), commands));
        await commands.DidNotReceiveWithAnyArgs().Create(default, default!, default);
        await commands.DidNotReceiveWithAnyArgs().Update(default, default!, default);
    }

    public static TheoryData<DownloadFolderSaveResult, int, string?> FolderOutcomes => new()
    {
        { new DownloadFolderSaveResult.Saved(new VersionedKey<Guid>(FolderId, 2)), 200, null },
        { new DownloadFolderSaveResult.NotFound(), 404, null },
        { new DownloadFolderSaveResult.ParentNotFound(), 404, null },
        { new DownloadFolderSaveResult.SlugTaken(), 409, "slug-taken" },
        { new DownloadFolderSaveResult.ParentDeleted(), 409, "parent-deleted" },
        { new DownloadFolderSaveResult.VersionConflict(), 409, "version-conflict" }
    };

    [Theory]
    [MemberData(nameof(FolderOutcomes))]
    public async Task UpdateFolder_MapsEveryOutcomeOfTheCommand(DownloadFolderSaveResult outcome, int status, string? reason)
    {
        var commands = Substitute.For<IDownloadFolderCommands>();
        commands.Update(FolderId, new IDownloadFolderCommands.FolderDraft("tools", "Tools"), Arg.Any<CancellationToken>())
            .Returns(outcome);

        var result = await DownloadEndpoints.UpdateFolder(
            FolderId,
            new DownloadEndpoints.SaveFolderRequest("Tools", "tools"),
            new DefaultHttpContext(),
            commands);

        AssertOutcome(result, status, reason);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task DeleteFolder_MapsTheOutcomeOfTheCommand(bool deleted, int status)
    {
        var commands = Substitute.For<IDownloadFolderCommands>();
        commands.Delete(FolderId, Arg.Any<CancellationToken>()).Returns(deleted);

        AssertOutcome(await DownloadEndpoints.DeleteFolder(FolderId, new DefaultHttpContext(), commands), status);
    }

    [Theory]
    [InlineData(typeof(DownloadFolderSaveResult.Saved), 204, null)]
    [InlineData(typeof(DownloadFolderSaveResult.NotFound), 404, null)]
    [InlineData(typeof(DownloadFolderSaveResult.SlugTaken), 409, "slug-taken")]
    [InlineData(typeof(DownloadFolderSaveResult.ParentDeleted), 409, "parent-deleted")]
    public async Task RestoreFolder_MapsEveryOutcomeOfTheCommand(Type outcome, int status, string? reason)
    {
        var commands = Substitute.For<IDownloadFolderCommands>();
        commands.Restore(FolderId, Arg.Any<CancellationToken>()).Returns(
            outcome == typeof(DownloadFolderSaveResult.Saved)
                ? new DownloadFolderSaveResult.Saved(new VersionedKey<Guid>(FolderId, 1))
                : (DownloadFolderSaveResult)Activator.CreateInstance(outcome)!);

        AssertOutcome(await DownloadEndpoints.RestoreFolder(FolderId, new DefaultHttpContext(), commands), status, reason);
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, 204)]
    [InlineData(HardDeleteResult.NotFound, 404)]
    [InlineData(HardDeleteResult.Rejected, 409)]
    public async Task HardDeleteFolder_MapsEveryOutcomeOfTheCommand(HardDeleteResult outcome, int status)
    {
        var commands = Substitute.For<IDownloadFolderCommands>();
        commands.HardDelete(FolderId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertOutcome(await DownloadEndpoints.HardDeleteFolder(FolderId, new DefaultHttpContext(), commands), status);
    }

    [Fact]
    public async Task Upload_StoresEveryFileWithItsDisplayNameTheUserAndTheContentType()
    {
        var added = new List<(Guid Folder, Guid User, IDownloadCommands.FileDraft Draft)>();

        var result = await UploadAs(
            Form([File([1, 2], "C:\\docs\\one.pdf"), File([3], "two.zip", "application/zip")], "First", "Second"),
            AddingCommands(added));

        var body = Assert.IsType<JsonHttpResult<DownloadUploadResponse>>(result).Value!;
        Assert.Equal(["one.pdf", "two.zip"], body.Files.Select(file => file.FileName));
        Assert.All(body.Files, file =>
        {
            Assert.NotNull(file.Id);
            Assert.Null(file.Error);
        });
        Assert.All(added, item => Assert.Equal((FolderId, UserId), (item.Folder, item.User)));
        Assert.Equal(["First", "Second"], added.Select(item => item.Draft.DisplayName));
        Assert.Equal(["one.pdf", "two.zip"], added.Select(item => item.Draft.FileName));
        Assert.Equal(["application/pdf", "application/zip"], added.Select(item => item.Draft.ContentType));
        Assert.Equal([[1, 2], [3]], added.Select(item => item.Draft.Content));
    }

    [Fact]
    public async Task Upload_MissingDisplayNames_DefaultToTheFileName()
    {
        var added = new List<(Guid Folder, Guid User, IDownloadCommands.FileDraft Draft)>();

        await UploadAs(Form([File([1], "one.pdf"), File([2], "two.pdf")], "Only first"), AddingCommands(added));

        Assert.Equal(["Only first", "two.pdf"], added.Select(item => item.Draft.DisplayName));
    }

    [Fact]
    public async Task Upload_BlankDisplayName_DefaultsToTheFileName()
    {
        var added = new List<(Guid Folder, Guid User, IDownloadCommands.FileDraft Draft)>();

        await UploadAs(Form([File([1], "one.pdf")], "   "), AddingCommands(added));

        Assert.Equal("one.pdf", Assert.Single(added).Draft.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a media type")]
    [InlineData("text/plain; charset=utf-8")]
    public async Task Upload_StoresOnlyAValidMediaTypeOfTheClient(string contentType)
    {
        var added = new List<(Guid Folder, Guid User, IDownloadCommands.FileDraft Draft)>();

        await UploadAs(Form([File([1], "one.bin", contentType)]), AddingCommands(added));

        Assert.Equal(
            contentType == "text/plain; charset=utf-8" ? "text/plain" : "application/octet-stream",
            Assert.Single(added).Draft.ContentType);
    }

    [Fact]
    public async Task Upload_RejectsOnlyTheEmptyAndTheTooLargeFiles()
    {
        var added = new List<(Guid Folder, Guid User, IDownloadCommands.FileDraft Draft)>();

        var result = await UploadAs(
            Form([File([], "empty.txt"), File(new byte[11], "big.bin"), File(new byte[10], "exact.bin"), File([1], "ok.txt")]),
            AddingCommands(added),
            options: Options(10));

        var body = Assert.IsType<JsonHttpResult<DownloadUploadResponse>>(result).Value!;
        Assert.Equal(
            [("empty.txt", "empty"), ("big.bin", "too-large"), ("exact.bin", null), ("ok.txt", null)],
            body.Files.Select(file => (file.FileName, file.Error)));
        Assert.Equal(["exact.bin", "ok.txt"], added.Select(item => item.Draft.FileName));
    }

    [Fact]
    public async Task Upload_ReportsAFileThatCouldNotBeSaved()
    {
        var result = await UploadAs(
            Form([File([1])]),
            AddingCommands([], () => new DownloadSaveResult.VersionConflict()));

        Assert.Equal(
            "version-conflict",
            Assert.Single(Assert.IsType<JsonHttpResult<DownloadUploadResponse>>(result).Value!.Files).Error);
    }

    [Fact]
    public async Task Upload_UnknownFolder_IsNotFoundAndStopsAtTheFirstFile()
    {
        var added = new List<(Guid Folder, Guid User, IDownloadCommands.FileDraft Draft)>();

        var result = await UploadAs(
            Form([File([1], "a.txt"), File([2], "b.txt")]),
            AddingCommands(added, () => new DownloadSaveResult.FolderNotFound()));

        Assert.IsType<NotFound>(result);
        Assert.Single(added);
    }

    [Fact]
    public async Task Upload_NoFilesOrTooManyFiles_IsABadRequest()
    {
        var commands = AddingCommands([]);
        var tooMany = Enumerable.Range(0, DownloadEndpoints.MaxUploadFiles + 1).Select(_ => File([1])).ToArray();

        Assert.IsType<BadRequest>(await UploadAs(null, commands));
        Assert.IsType<BadRequest>(await UploadAs(Form([]), commands));
        Assert.IsType<BadRequest>(await UploadAs(Form(tooMany), commands));
        await commands.DidNotReceiveWithAnyArgs().Add(default, default, default!, default);
    }

    [Fact]
    public async Task Upload_WithoutAValidSession_IsUnauthorized()
    {
        var commands = AddingCommands([]);

        Assert.IsType<UnauthorizedHttpResult>(await UploadAs(Form([File([1])]), commands, valid: false));
        await commands.DidNotReceiveWithAnyArgs().Add(default, default, default!, default);
    }

    [Fact]
    public async Task Rename_MapsEveryOutcomeOfTheCommand()
    {
        var commands = Substitute.For<IDownloadCommands>();
        commands.Rename(DownloadId, "Handbook", Arg.Any<CancellationToken>())
            .Returns(new DownloadSaveResult.Saved(new VersionedKey<Guid>(DownloadId, 2)));

        AssertOutcome(
            await DownloadEndpoints.Rename(
                DownloadId,
                new DownloadEndpoints.RenameDownloadRequest("Handbook"),
                new DefaultHttpContext(),
                commands),
            200);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rename_BlankName_IsABadRequest(string name)
    {
        var commands = Substitute.For<IDownloadCommands>();

        Assert.IsType<BadRequest>(
            await DownloadEndpoints.Rename(
                DownloadId,
                new DownloadEndpoints.RenameDownloadRequest(name),
                new DefaultHttpContext(),
                commands));
        await commands.DidNotReceiveWithAnyArgs().Rename(default, default!, default);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Delete_MapsTheOutcomeOfTheCommand(bool deleted, int status)
    {
        var commands = Substitute.For<IDownloadCommands>();
        commands.Delete(DownloadId, Arg.Any<CancellationToken>()).Returns(deleted);

        AssertOutcome(await DownloadEndpoints.Delete(DownloadId, new DefaultHttpContext(), commands), status);
    }

    [Theory]
    [InlineData(typeof(DownloadSaveResult.Saved), 204, null)]
    [InlineData(typeof(DownloadSaveResult.NotFound), 404, null)]
    [InlineData(typeof(DownloadSaveResult.FolderDeleted), 409, "folder-deleted")]
    public async Task Restore_MapsEveryOutcomeOfTheCommand(Type outcome, int status, string? reason)
    {
        var commands = Substitute.For<IDownloadCommands>();
        commands.Restore(DownloadId, Arg.Any<CancellationToken>()).Returns(
            outcome == typeof(DownloadSaveResult.Saved)
                ? new DownloadSaveResult.Saved(new VersionedKey<Guid>(DownloadId, 1))
                : (DownloadSaveResult)Activator.CreateInstance(outcome)!);

        AssertOutcome(await DownloadEndpoints.Restore(DownloadId, new DefaultHttpContext(), commands), status, reason);
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, 204)]
    [InlineData(HardDeleteResult.NotFound, 404)]
    [InlineData(HardDeleteResult.Rejected, 409)]
    public async Task HardDelete_MapsEveryOutcomeOfTheCommand(HardDeleteResult outcome, int status)
    {
        var commands = Substitute.For<IDownloadCommands>();
        commands.HardDelete(DownloadId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertOutcome(await DownloadEndpoints.HardDelete(DownloadId, new DefaultHttpContext(), commands), status);
    }
}
