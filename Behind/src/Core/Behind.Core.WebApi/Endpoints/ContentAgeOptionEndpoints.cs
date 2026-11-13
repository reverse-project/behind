using System.ComponentModel.DataAnnotations;
using System.Data;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Behind.Core.WebApi.Endpoints;

public static class ContentAgeOptionEndpoints
{
    public sealed record CreateRequest([property: Range(1, ContentAgePolicy.MaximumAge)] int Age, string? Name);

    public sealed record UpdateRequest(string? Name);

    public sealed record AgeOptionResponse(Guid Id, int Age, string Name);

    public sealed record AgeOptionListResponse(IReadOnlyList<AgeOptionResponse> Items);

    [IgnoreETag]
    public static async Task<IResult> ListAuthorized(
        HttpContext httpContext, AbstractCoreContext context, IAuthorizationService authorization, bool deleted = false)
    {
        var canManage = await authorization.HoldsGrantAsync(httpContext.User, "ContentAge.Manage");
        if (!canManage && (deleted || !await authorization.HoldsGrantAsync(httpContext.User, "ContentRestriction.Write")))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (deleted && !await authorization.HoldsGrantAsync(httpContext.User, "RawDatabase.Manage"))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        return await List(httpContext, context, deleted);
    }

    [IgnoreETag]
    public static async Task<IResult> List(
        HttpContext httpContext, AbstractCoreContext context, bool deleted = false)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
        var state = deleted ? DeletionState.Deleted : DeletionState.Active;
        var options = await context.ContentAgeOptions
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.ContentAgeOptions)
            .OrderBy(item => item.Age)
            .Select(item => new AgeOptionResponse(EF.Property<Guid>(item, VersionedColumns.Key), item.Age, item.Name))
            .ToListAsync(httpContext.RequestAborted);
        return Results.Json(new AgeOptionListResponse(options));
    }

    [IgnoreETag]
    public static async Task<IResult> Create(
        CreateRequest request, HttpContext httpContext, AbstractCoreContext context)
    {
        var name = ValidName(request.Name);
        if (request.Age is < 1 or > ContentAgePolicy.MaximumAge || name is null)
        {
            return Results.BadRequest();
        }

        return await ExecuteMutation(context, httpContext.RequestAborted, async () =>
        {
            if (await context.IsActiveContentAgeOption(request.Age, httpContext.RequestAborted))
            {
                return Results.Conflict();
            }

            var id = Guid.NewGuid();
            var option = new ContentAgeOption(new VersionedKey<Guid>(id, 1), request.Age, name);
            context.ContentAgeOptions.Add(option);
            context.Entry(option).StampVersionedKey(id, 1);
            await context.SaveChangesAsync(httpContext.RequestAborted);
            return Results.Created("/content-age-options", new AgeOptionResponse(id, request.Age, name));
        });
    }

    [IgnoreETag]
    public static async Task<IResult> Update(
        Guid id, UpdateRequest request, HttpContext httpContext, AbstractCoreContext context)
    {
        var name = ValidName(request.Name);
        if (name is null)
        {
            return Results.BadRequest();
        }

        return await ExecuteMutation(context, httpContext.RequestAborted, async () =>
        {
            var option = await context.ActiveContentAgeOptions.OfKey(id)
                .SingleOrDefaultAsync(httpContext.RequestAborted);
            if (option is null)
            {
                return Results.NotFound();
            }

            var previous = context.Entry(option).VersionedId;
            var next = new ContentAgeOption(previous with { Version = previous.Version + 1 }, option.Age, name);
            context.ContentAgeOptions.Add(next);
            context.Entry(next).StampVersionedKey(previous.Key, previous.Version + 1);
            await context.SaveChangesAsync(httpContext.RequestAborted);
            return Results.Ok(new AgeOptionResponse(id, option.Age, name));
        });
    }

    private static string? ValidName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200)
        {
            return null;
        }

        for (var index = 0; index < name.Length; index++)
        {
            if (char.IsControl(name[index]))
            {
                return null;
            }
            if (char.IsSurrogate(name[index]))
            {
                if (!char.IsHighSurrogate(name[index]) || index + 1 == name.Length
                    || !char.IsLowSurrogate(name[index + 1]))
                {
                    return null;
                }
                index++;
            }
        }
        return name;
    }

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, AbstractCoreContext context) =>
        await ExecuteMutation(context, httpContext.RequestAborted, async () =>
        {
            var option = await context.ActiveContentAgeOptions.OfKey(id)
                .SingleOrDefaultAsync(httpContext.RequestAborted);
            if (option is null)
            {
                return Results.NotFound();
            }

            await context.MarkSoftDeletedAsync(context.ContentAgeOptions.OfKey(id), httpContext.RequestAborted);
            await context.SaveChangesAsync(httpContext.RequestAborted);
            return Results.NoContent();
        });

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, AbstractCoreContext context) =>
        await ExecuteMutation(context, httpContext.RequestAborted, async () =>
        {
            ContentAgeOption? option;
            using (context.SoftDelete.IncludeDeleted())
            {
                option = await context.ContentAgeOptions
                    .InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Deleted)
                    .LatestOf(context.ContentAgeOptions).OfKey(id)
                    .SingleOrDefaultAsync(httpContext.RequestAborted);
            }

            if (option is null)
            {
                return Results.NotFound();
            }

            if (await context.ActiveContentAgeOptions.AnyAsync(
                    item => item.Age == option.Age, httpContext.RequestAborted))
            {
                return Results.Conflict();
            }

            await context.MarkRestoredAsync(context.ContentAgeOptions.OfKey(id), httpContext.RequestAborted);
            await context.SaveChangesAsync(httpContext.RequestAborted);
            return Results.NoContent();
        });

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, AbstractCoreContext context) =>
        await ExecuteMutation(context, httpContext.RequestAborted, async () =>
        {
            var changed = await context.MarkHardDeletedAsync(context.ContentAgeOptions.OfKey(id),
                httpContext.RequestAborted);
            await context.SaveChangesAsync(httpContext.RequestAborted);
            return changed > 0 ? Results.NoContent() : Results.NotFound();
        });

    public static async Task<IResult> ExecuteMutation(
        AbstractCoreContext context, CancellationToken cancellationToken, Func<Task<IResult>> action)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (IsMutationConflict(exception))
        {
            context.ChangeTracker.Clear();
            return Results.Conflict();
        }
    }

    private static bool IsMutationConflict(Exception exception) => exception switch
    {
        DbUpdateException { InnerException: not null } update => IsMutationConflict(update.InnerException!),
        SqliteException sqlite => sqlite.SqliteErrorCode is 5 or 6
                                  || sqlite.SqliteExtendedErrorCode is 1555 or 2067,
        PostgresException postgres => postgres.SqlState is "40001" or "40P01" or "23505",
        _ => false
    };

    extension(IEndpointRouteBuilder application)
    {
        public void MapContentAgeOptionEndpoints()
        {
            application.MapGet("/content-age-options", ListAuthorized).RequireAuthorization();
            application.MapPost("/content-age-options", Create).RequireAuthorization("ContentAge.Manage")
                .AddEndpointFilter<DataAnnotationValidationFilter<CreateRequest>>();
            var group = application.MapGroup("/content-age-options").RequireAuthorization("ContentAge.Manage");
            group.MapPost("/{id:guid}/update", Update);
            group.MapPost("/{id:guid}/delete", Delete);
            group.MapPost("/{id:guid}/restore", Restore).RequireAuthorization("RawDatabase.Manage");
            group.MapPost("/{id:guid}/hard/delete", HardDelete).RequireAuthorization("RawDatabase.Manage");
        }
    }
}
