using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class RawDatabaseRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapRawDatabaseEndpoints()
        {
            var manage = ManagedListExtensions.DeletedRecordsGrant;

            application.MapGet("/raw-database/tables", RawDatabaseEndpoints.Tables)
                .RequireAuthorization(manage)
                .Produces<RawTablesResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/raw-database/tables/{table}/rows", RawDatabaseEndpoints.Rows)
                .RequireAuthorization(manage)
                .Produces<PageResponse<IReadOnlyDictionary<string, object?>>>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/raw-database/tables/{table}/rows", RawDatabaseEndpoints.Insert)
                .RequireAuthorization(manage)
                .AddEndpointFilter<DataAnnotationValidationFilter<RawDatabaseEndpoints.InsertRowRequest>>()
                .Produces<IReadOnlyDictionary<string, object?>>()
                .Produces<RawInvalidResponse>(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<RawConflictResponse>(409)
                .Produces(500);

            application.MapPost("/raw-database/tables/{table}/rows/update", RawDatabaseEndpoints.Update)
                .RequireAuthorization(manage)
                .AddEndpointFilter<DataAnnotationValidationFilter<RawDatabaseEndpoints.UpdateRowRequest>>()
                .Produces<IReadOnlyDictionary<string, object?>>()
                .Produces<RawInvalidResponse>(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<RawConflictResponse>(409)
                .Produces(500);

            application.MapPost("/raw-database/tables/{table}/rows/delete", RawDatabaseEndpoints.Delete)
                .RequireAuthorization(manage)
                .AddEndpointFilter<DataAnnotationValidationFilter<RawDatabaseEndpoints.DeleteRowRequest>>()
                .Produces(204)
                .Produces<RawInvalidResponse>(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<RawConflictResponse>(409)
                .Produces(500);
        }
    }
}
