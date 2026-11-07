using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public static class DownloadRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapDownloadEndpoints()
        {
            var write = GrantName.Of(GrantArea.Download, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.Download, GrantAction.Delete);

            application.MapGet("/downloads/folders", DownloadEndpoints.ListFolders)
                .Produces<DownloadFoldersResponse>()
                .Produces(500);

            application.MapGet("/downloads/folders/{id:guid}/files", DownloadEndpoints.ListFiles)
                .Produces<DownloadFilesResponse>()
                .Produces(404)
                .Produces(500);

            application.MapGet("/downloads/deleted", DownloadEndpoints.ListDeleted)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces<DownloadDeletedResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet($"{DownloadResponseExtensions.FilePath}/{{id:guid}}", DownloadEndpoints.Get)
                .Produces(200)
                .Produces(404)
                .Produces(500);

            application.MapPost("/downloads/folders", DownloadEndpoints.CreateFolder)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<DownloadEndpoints.SaveFolderRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/downloads/folders/{id:guid}/folders", DownloadEndpoints.CreateSubfolder)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<DownloadEndpoints.SaveFolderRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/downloads/folders/{id:guid}", DownloadEndpoints.UpdateFolder)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<DownloadEndpoints.SaveFolderRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/downloads/folders/{id:guid}/delete", DownloadEndpoints.DeleteFolder)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/downloads/folders/{id:guid}/restore", DownloadEndpoints.RestoreFolder)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/downloads/folders/{id:guid}/hard/delete", DownloadEndpoints.HardDeleteFolder)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);

            application.MapPost("/downloads/folders/{id:guid}/files", DownloadEndpoints.Upload)
                .RequireAuthorization(write)
                .DisableAntiforgery()
                .WithMetadata(new DownloadUploadLimitMetadata())
                .Produces<DownloadUploadResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/downloads/files/{id:guid}", DownloadEndpoints.Rename)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<DownloadEndpoints.RenameDownloadRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/downloads/files/{id:guid}/delete", DownloadEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/downloads/files/{id:guid}/restore", DownloadEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/downloads/files/{id:guid}/hard/delete", DownloadEndpoints.HardDelete)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);
        }
    }
}
