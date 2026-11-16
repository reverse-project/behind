using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class ScheduleRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapScheduleEndpoints()
        {
            var read = GrantName.Of(GrantArea.Schedule, GrantAction.Read);
            var write = GrantName.Of(GrantArea.Schedule, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.Schedule, GrantAction.Delete);
            var manage = GrantName.Of(GrantArea.Schedule, GrantAction.Manage);

            application.MapGet("/schedules/manage", ScheduleEndpoints.Manage)
                .RequireAuthorization(read)
                .Produces<ScheduleManageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/schedules/actions", ScheduleEndpoints.Actions)
                .RequireAuthorization(read)
                .Produces<IReadOnlyList<ScheduledActionResponse>>()
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/schedules/actions/{key}/options/{parameter}", ScheduleEndpoints.Options)
                .RequireAuthorization(write)
                .Produces<IReadOnlyList<ScheduledActionOption>>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapGet("/schedules/cron", ScheduleEndpoints.Cron)
                .RequireAuthorization(read)
                .Produces<ScheduleCronResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/schedules/participants", ScheduleEndpoints.Participants)
                .RequireAuthorization(write)
                .Produces<ScheduleParticipantDirectoryResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/schedules/runs", ScheduleEndpoints.Runs)
                .RequireAuthorization(read)
                .Produces<PageResponse<ScheduleRunResponse>>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost("/schedules/runs/{id:guid}/delete", ScheduleEndpoints.DeleteRun)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/schedules/runs/{id:guid}/restore", ScheduleEndpoints.RestoreRun)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/schedules/runs/{id:guid}/hard/delete", ScheduleEndpoints.HardDeleteRun)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);

            application.MapGet("/schedules/{id:guid}", ScheduleEndpoints.Get)
                .RequireAuthorization(read)
                .Produces<ScheduleDetailsResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/schedules", ScheduleEndpoints.Create)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<ScheduleEndpoints.SaveScheduleRequest>>()
                .Produces<VersionedSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<VersionedConflictResponse>(409)
                .Produces(500);

            application.MapPost("/schedules/{id:guid}", ScheduleEndpoints.Update)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<ScheduleEndpoints.SaveScheduleRequest>>()
                .Produces<VersionedSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<VersionedConflictResponse>(409)
                .Produces(500);

            application.MapPost("/schedules/{id:guid}/run", ScheduleEndpoints.Run)
                .RequireAuthorization(manage)
                .Produces<ScheduleRunStartedResponse>(202)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<VersionedConflictResponse>(409)
                .Produces(500);

            application.MapPost("/schedules/{id:guid}/delete", ScheduleEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/schedules/{id:guid}/restore", ScheduleEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/schedules/{id:guid}/hard/delete", ScheduleEndpoints.HardDelete)
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
