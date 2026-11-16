using Behind.Common.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi;

public sealed record VersionedSavedResponse(Guid Id, long Version);

public sealed record VersionedConflictResponse(string Reason);

public static class VersionedSaveResultExtensions
{
    public const string VersionConflictReason = "version-conflict";

    extension(VersionedSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                VersionedSaveResult.Saved saved => Results.Json(new VersionedSavedResponse(saved.Id.Key, saved.Id.Version)),
                VersionedSaveResult.NotFound => Results.NotFound(),
                VersionedSaveResult.Rejected rejected => Results.Conflict(new VersionedConflictResponse(rejected.Reason)),
                _ => Results.Conflict(new VersionedConflictResponse(VersionConflictReason))
            };
    }

    extension(bool found)
    {
        public IResult ToNoContentResult() => found ? Results.NoContent() : Results.NotFound();
    }
}
