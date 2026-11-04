using Behind.Common.Database.Raw;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class RawDatabaseResponseExtensions
{
    public const string ReadOnlyReason = "read-only";
    public const string RejectedReason = "rejected";

    extension(RawColumn column)
    {
        public RawColumnResponse ToResponse() =>
            new(
                column.Name,
                column.StoreType,
                column.Kind.ToString().ToLowerInvariant(),
                column.IsNullable,
                column.IsKey,
                column.IsSensitive);
    }

    extension(IRawDatabaseQueries.RawTableListing listing)
    {
        public RawTableResponse ToResponse() =>
            new(
                listing.Table.Name,
                listing.Table.EntityName,
                listing.IsReadOnly,
                listing.Rows,
                [..listing.Table.Columns.Select(column => column.ToResponse())]);
    }

    extension(RawWriteResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                RawWriteResult.Saved saved => Results.Json(saved.Row.Values),
                RawWriteResult.Deleted => Results.NoContent(),
                RawWriteResult.NotFound => Results.NotFound(),
                RawWriteResult.ReadOnly => Results.Json(new RawConflictResponse(ReadOnlyReason, null), statusCode: 409),
                RawWriteResult.Invalid invalid => Results.Json(new RawInvalidResponse(invalid.Errors), statusCode: 400),
                RawWriteResult.Rejected rejected =>
                    Results.Json(new RawConflictResponse(RejectedReason, rejected.Message), statusCode: 409),
                _ => Results.StatusCode(500)
            };
    }
}
