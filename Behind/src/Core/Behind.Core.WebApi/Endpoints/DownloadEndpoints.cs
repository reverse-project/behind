using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class DownloadEndpoints
{
    public const int MaxUploadFiles = 20;
    public const string DisplayNamesField = "displayNames";

    public sealed record SaveFolderRequest(
        [property: Required, MaxLength(DownloadFolder.MaxDisplayNameLength)] string DisplayName,
        [property: MaxLength(DownloadFolder.MaxSlugLength * 2)] string? Slug);

    public sealed record RenameDownloadRequest(
        [property: Required, MaxLength(Download.MaxDisplayNameLength)] string DisplayName);

    public static async Task<IResult> ListFolders(
        HttpContext httpContext,
        IDownloadQueries queries,
        IOptionsSnapshot<DownloadsConfiguration> configuration)
    {
        httpContext.Response.Headers.CacheControl = AttachmentResponseExtensions.Revalidate;

        var folders = await queries.ListFolders(httpContext.RequestAborted);

        return Results.Json(new DownloadFoldersResponse(
            configuration.Value.FileLimit,
            folders.Select(folder => folder.ToItem()).ToList()));
    }

    public static async Task<IResult> ListFiles(Guid id, HttpContext httpContext, IDownloadQueries queries)
    {
        httpContext.Response.Headers.CacheControl = AttachmentResponseExtensions.Revalidate;

        var files = await queries.ListFiles(id, httpContext.RequestAborted);

        return files.Match(
            () => Results.NotFound(),
            found => Results.Json(new DownloadFilesResponse(found.Select(file => file.ToItem()).ToList())));
    }

    [IgnoreETag]
    public static async Task<IResult> Get(Guid id, HttpContext httpContext, IDownloadQueries queries)
    {
        var content = await queries.Open(id, httpContext.RequestAborted);

        return content.Match(
            () => Results.NotFound(),
            found => httpContext.ToAttachment(found.File.FileName, found.File.ContentType, found.Bytes));
    }

    [IgnoreETag]
    public static async Task<IResult> ListDeleted(HttpContext httpContext, IDownloadQueries queries)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        return Results.Json((await queries.ListDeleted(httpContext.RequestAborted)).ToResponse());
    }

    [IgnoreETag]
    public static Task<IResult> CreateFolder(
        SaveFolderRequest request,
        HttpContext httpContext,
        IDownloadFolderCommands commands) =>
        SaveFolder(request, draft => commands.Create(None, draft, httpContext.RequestAborted));

    [IgnoreETag]
    public static Task<IResult> CreateSubfolder(
        Guid id,
        SaveFolderRequest request,
        HttpContext httpContext,
        IDownloadFolderCommands commands) =>
        SaveFolder(request, draft => commands.Create(id, draft, httpContext.RequestAborted));

    [IgnoreETag]
    public static Task<IResult> UpdateFolder(
        Guid id,
        SaveFolderRequest request,
        HttpContext httpContext,
        IDownloadFolderCommands commands) =>
        SaveFolder(request, draft => commands.Update(id, draft, httpContext.RequestAborted));

    [IgnoreETag]
    public static async Task<IResult> DeleteFolder(Guid id, HttpContext httpContext, IDownloadFolderCommands commands) =>
        await commands.Delete(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> RestoreFolder(Guid id, HttpContext httpContext, IDownloadFolderCommands commands) =>
        (await commands.Restore(id, httpContext.RequestAborted)).ToRestoreResult();

    [IgnoreETag]
    public static async Task<IResult> HardDeleteFolder(
        Guid id,
        HttpContext httpContext,
        IDownloadFolderCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Upload(
        Guid id,
        IFormCollection? form,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        IDownloadCommands commands,
        IOptionsSnapshot<DownloadsConfiguration> configuration)
    {
        var files = form?.Files;

        if (files is null || files.Count == 0 || files.Count > MaxUploadFiles)
        {
            return Results.BadRequest();
        }

        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                var names = form!.TryGetValue(DisplayNamesField, out var values) ? values.ToArray() : [];
                var uploaded = new List<DownloadUploadedFile>();

                foreach (var (file, index) in files.Select((file, index) => (file, index)))
                {
                    var stored = await Store(
                        id,
                        session.UserId,
                        file,
                        index < names.Length ? names[index] : null,
                        httpContext,
                        commands,
                        configuration.Value.FileLimit);

                    if (stored.Error == DownloadResponseExtensions.FolderNotFoundError)
                    {
                        return Results.NotFound();
                    }

                    uploaded.Add(stored);
                }

                return Results.Json(new DownloadUploadResponse(uploaded));
            });
    }

    [IgnoreETag]
    public static async Task<IResult> Rename(
        Guid id,
        RenameDownloadRequest request,
        HttpContext httpContext,
        IDownloadCommands commands) =>
        string.IsNullOrWhiteSpace(request.DisplayName)
            ? Results.BadRequest()
            : (await commands.Rename(id, request.DisplayName, httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, IDownloadCommands commands) =>
        await commands.Delete(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IDownloadCommands commands) =>
        (await commands.Restore(id, httpContext.RequestAborted)).ToRestoreResult();

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, IDownloadCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();

    private static Task<IResult> SaveFolder(
        SaveFolderRequest request,
        Func<IDownloadFolderCommands.FolderDraft, Task<DownloadFolderSaveResult>> save) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await save(draft)).ToResult());

    private static async Task<DownloadUploadedFile> Store(
        Guid folderId,
        Guid userId,
        IFormFile file,
        string? displayName,
        HttpContext httpContext,
        IDownloadCommands commands,
        long limit)
    {
        var fileName = file.FileName.ToVfsFileName("file");

        if (file.Length == 0)
        {
            return new DownloadUploadedFile(fileName, null, DownloadResponseExtensions.EmptyFileError);
        }

        if (file.Length > limit)
        {
            return new DownloadUploadedFile(fileName, null, DownloadResponseExtensions.TooLargeFileError);
        }

        var result = await commands.Add(
            folderId,
            userId,
            new IDownloadCommands.FileDraft(
                displayName.ToDownloadDisplayName(fileName),
                fileName,
                file.ToStoredContentType(VfsFile.MaxContentTypeLength),
                await file.ReadAllBytesAsync(httpContext.RequestAborted)),
            httpContext.RequestAborted);

        return result switch
        {
            DownloadSaveResult.Saved saved => new DownloadUploadedFile(fileName, saved.Id.Key, null),
            DownloadSaveResult.FolderNotFound => new DownloadUploadedFile(
                fileName,
                null,
                DownloadResponseExtensions.FolderNotFoundError),
            _ => new DownloadUploadedFile(fileName, null, NewsResponseExtensions.VersionConflictReason)
        };
    }
}
