using Behind.Common;
using Behind.Common.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.WebApi;

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

            return await httpContext.WithDeletionState(
                authorization,
                deleted,
                state => execute(new ManagedListQuery(search.ToSearchTerm(MaxSearchLength), letterFilter, state)));
        }

        public async Task<IResult> WithDeletionState(
            IAuthorizationService authorization,
            bool deleted,
            Func<DeletionState, Task<IResult>> execute)
        {
            httpContext.Response.Headers.CacheControl = NoStore;

            if (deleted && !await authorization.HoldsGrantAsync(httpContext.User, DeletedRecordsGrant))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return await execute(deleted ? DeletionState.Deleted : DeletionState.Active);
        }
    }
}
