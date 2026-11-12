using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public static class ContentRestrictionEndpoints
{
    public sealed record SaveRequest(
        [property: Required, MaxLength(200)] string EntityType,
        Guid EntityId,
        [property: Range(0, ContentAgePolicy.MaximumAge)] int MinimumAge);

    [IgnoreETag]
    public static async Task<IResult> Browse(
        string entityType,
        HttpContext httpContext,
        AbstractCoreContext context,
        int? page = null,
        string? search = null)
    {
        var type = context.RestrictedContentTypes.SingleOrDefault(type => type.Name == entityType);
        if (type is null || search?.Length > 200)
        {
            return Results.BadRequest();
        }

        httpContext.Response.Headers.CacheControl = "no-store";
        return Results.Json(await context.BrowseContentItems(
            type, Flipper.Create(page, 20, 20, 20), search, httpContext.RequestAborted));
    }

    [IgnoreETag]
    public static async Task<IResult> List(
        HttpContext httpContext,
        AbstractCoreContext context,
        IOptionsMonitor<ContentRestrictionsConfiguration> configuration,
        IEnumerable<IAuditLabelSource> labelSources,
        bool deleted = false)
    {
        using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
        var state = deleted ? DeletionState.Deleted : DeletionState.Active;
        var rows = await context.ContentRestrictions
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.ContentRestrictions)
            .Select(item => new
            {
                Id = EF.Property<Guid>(item, VersionedColumns.Key),
                item.EntityType, item.EntityId, item.MinimumAge
            })
            .ToListAsync(httpContext.RequestAborted);
        var labels = await context.ResolveAuditLabelsAsync(
            rows.Select(item => item.EntityId).ToArray(), labelSources, httpContext.RequestAborted);
        return Results.Json(new
        {
            Enabled = configuration.CurrentValue.Enabled,
            Types = context.RestrictedContentTypes.Select(type => type.Name).Order().ToArray(),
            Items = rows.Select(item => new
            {
                item.Id, item.EntityType, item.EntityId, item.MinimumAge,
                DisplayName = string.IsNullOrWhiteSpace(labels.Label(item.EntityId.ToString()))
                    ? item.EntityId.ToString()
                    : labels.Label(item.EntityId.ToString())
            })
        });
    }

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, AbstractCoreContext context)
    {
        var changed = await context.MarkSoftDeletedAsync(context.ContentRestrictions.OfKey(id), httpContext.RequestAborted);
        await context.SaveChangesAsync(httpContext.RequestAborted);
        return changed > 0 ? Results.NoContent() : Results.NotFound();
    }

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, AbstractCoreContext context)
    {
        ContentRestriction? restriction;
        using (context.SoftDelete.IncludeDeleted())
        {
            restriction = await context.ContentRestrictions.OfKey(id)
                .OrderByDescending(item => EF.Property<long>(item, VersionedColumns.Version))
                .FirstOrDefaultAsync(httpContext.RequestAborted);
        }

        if (restriction is null)
        {
            return Results.NotFound();
        }

        if (await context.ContentRestrictions.AnyAsync(item =>
                item.EntityType == restriction.EntityType && item.EntityId == restriction.EntityId,
                httpContext.RequestAborted))
        {
            return Results.Conflict();
        }

        var changed = await context.MarkRestoredAsync(context.ContentRestrictions.OfKey(id), httpContext.RequestAborted);
        await context.SaveChangesAsync(httpContext.RequestAborted);
        return changed > 0 ? Results.NoContent() : Results.NotFound();
    }

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, AbstractCoreContext context)
    {
        var changed = await context.MarkHardDeletedAsync(context.ContentRestrictions.OfKey(id), httpContext.RequestAborted);
        await context.SaveChangesAsync(httpContext.RequestAborted);
        return changed > 0 ? Results.NoContent() : Results.NotFound();
    }

    [IgnoreETag]
    public static async Task<IResult> Get(
        string entityType,
        Guid id,
        HttpContext httpContext,
        AbstractCoreContext context,
        IOptionsMonitor<ContentRestrictionsConfiguration> configuration)
    {
        if (!context.RestrictedContentTypes.Any(type => type.Name == entityType))
        {
            return Results.BadRequest();
        }

        var minimumAge = await context.ContentRestrictions.LatestOf(context.ContentRestrictions)
            .Where(item => item.EntityType == entityType && item.EntityId == id)
            .Select(item => (int?)item.MinimumAge).SingleOrDefaultAsync(httpContext.RequestAborted);
        return Results.Json(new { MinimumAge = minimumAge ?? 0, Enabled = configuration.CurrentValue.Enabled });
    }

    [IgnoreETag]
    public static async Task<IResult> Save(SaveRequest request, HttpContext httpContext, AbstractCoreContext context)
    {
        if (request.EntityId == Guid.Empty || request.MinimumAge is < 0 or > ContentAgePolicy.MaximumAge
            || !context.RestrictedContentTypes.Any(type => type.Name == request.EntityType))
        {
            return Results.BadRequest();
        }

        var previous = await context.ContentRestrictions
            .Where(item => item.EntityType == request.EntityType && item.EntityId == request.EntityId)
            .OrderByDescending(item => EF.Property<long>(item, VersionedColumns.Version))
            .Select(item => new
            {
                Key = EF.Property<Guid>(item, VersionedColumns.Key),
                Version = EF.Property<long>(item, VersionedColumns.Version)
            }).FirstOrDefaultAsync(httpContext.RequestAborted);
        var key = previous?.Key ?? Guid.NewGuid();
        var version = (previous?.Version ?? 0) + 1;
        var restriction = new ContentRestriction(new VersionedKey<Guid>(key, version),
            request.EntityType, request.EntityId, request.MinimumAge);
        context.ContentRestrictions.Add(restriction);
        context.Entry(restriction).StampVersionedKey(key, version);
        await context.SaveChangesAsync(httpContext.RequestAborted);
        return Results.NoContent();
    }

    extension(IEndpointRouteBuilder application)
    {
        public void MapContentRestrictionEndpoints()
        {
            var group = application.MapGroup("/content-restrictions")
                .RequireAuthorization("RawDatabase.Manage");
            group.MapGet("", List);
            group.MapGet("/browse/{entityType}", Browse);
            group.MapGet("/{entityType}/{id:guid}", Get);
            group.MapPost("", Save);
            group.MapPost("/{id:guid}/delete", Delete);
            group.MapPost("/{id:guid}/restore", Restore);
            group.MapPost("/{id:guid}/hard/delete", HardDelete);
        }
    }
}
