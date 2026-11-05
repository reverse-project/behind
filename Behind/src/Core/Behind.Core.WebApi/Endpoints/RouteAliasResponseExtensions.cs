using Behind.Common.Database;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class RouteAliasResponseExtensions
{
    public const string AliasTakenReason = "alias-taken";

    extension(IRouteAliasQueries.RouteAliasListing alias)
    {
        public ManagedRouteAlias ToItem(DeletionState state) =>
            new(alias.Id.Key, alias.Alias, alias.Target, state == DeletionState.Deleted);
    }

    extension(RouteAliasSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                RouteAliasSaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                RouteAliasSaveResult.NotFound => Results.NotFound(),
                RouteAliasSaveResult.AliasTaken => Conflict(AliasTakenReason),
                _ => Conflict(NewsResponseExtensions.VersionConflictReason)
            };
    }

    private static IResult Conflict(string reason) => Results.Conflict(new NewsConflictResponse(reason));
}
