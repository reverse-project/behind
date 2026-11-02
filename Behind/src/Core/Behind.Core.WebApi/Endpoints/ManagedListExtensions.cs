using Behind.Common;
using Behind.Common.Database;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed record ManagedListQuery(Option<string> Search, Option<string> Letter, DeletionState State);

public static class ManagedListExtensions
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;
    public const string DeletedRecordsGrant = "RawDatabase.Manage";
    public const string NoStore = "no-store";

    extension(HttpContext httpContext)
    {
        public async Task<IResult> WithManagedList(
            IAuthorizationService authorization,
            string? search,
            string? letter,
            bool deleted,
            Func<ManagedListQuery, Task<IResult>> execute)
        {
            httpContext.Response.Headers.CacheControl = NoStore;

            var letterFilter = LetterBucketExtensions.ParseLetterBucket(letter);

            if (!string.IsNullOrWhiteSpace(letter) && letterFilter == None)
            {
                return Results.BadRequest();
            }

            if (deleted && !await authorization.HoldsGrantAsync(httpContext.User, DeletedRecordsGrant))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return await execute(new ManagedListQuery(
                search.ToSearchTerm(MaxSearchLength),
                letterFilter,
                deleted ? DeletionState.Deleted : DeletionState.Active));
        }
    }
}
