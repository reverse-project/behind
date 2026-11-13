using System.ComponentModel.DataAnnotations;
using Behind.Common.Database;
using Behind.Common.Modules;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.WebApi.Endpoints;

public static class ContentLocaleEndpoints
{
    public sealed record SaveRequest(
        [property: Required, MaxLength(200)] string EntityType,
        Guid EntityId,
        [property: MaxLength(100)] string? Locale);
    public sealed record QueryItem(string EntityType, Guid EntityId);
    public sealed record QueryRequest(IReadOnlyList<QueryItem> Items);
    public sealed record QueryResult(
        string EntityType, Guid EntityId, int Status, string? Locale, string? EffectiveLocale);

    private static readonly IReadOnlyDictionary<string, string> CorePolicies = new Dictionary<string, string>
    {
        [nameof(Site)] = "Site.Write",
        [nameof(SiteSection)] = "Site.Write",
        [nameof(News)] = "News.Write",
        [nameof(NewsCategory)] = "News.Write",
        [nameof(Article)] = "Article.Write",
        [nameof(ArticleCategory)] = "Article.Write",
        [nameof(Gallery)] = "Gallery.Write",
        [nameof(GalleryItem)] = "Gallery.Write",
        [nameof(Carousel)] = "Site.Write",
        [nameof(CarouselSlide)] = "Site.Write",
        [nameof(Download)] = "Download.Write",
        [nameof(DownloadFolder)] = "Download.Write",
        [nameof(Menu)] = "Menu.Write",
        [nameof(MenuItem)] = "Menu.Write",
        [nameof(RouteAlias)] = "RouteAlias.Write",
        [nameof(Message)] = "Message.Write"
    };

    [IgnoreETag]
    public static async Task<IResult> List(LocaleCatalog locales, CancellationToken cancellationToken) =>
        Results.Json(new { Locales = (await locales.ListAsync(cancellationToken)).Select(item => item.Code).ToArray() });

    [IgnoreETag]
    public static async Task<IResult> Get(
        string entityType, Guid id, HttpContext http, AbstractCoreContext context,
        IAuthorizationService authorization, IEnumerable<IContentLocalePermission> permissions)
    {
        var error = await Check(entityType, id, http, context, authorization, permissions);
        if (error is not null)
        {
            return error;
        }

        var assignments = await context.ContentLocales.LatestOf(context.ContentLocales)
            .Select(item => new ContentLocaleItem(item.EntityType, item.EntityId, item.Locale))
            .ToListAsync(http.RequestAborted);
        var scope = await context.ResolveContentLocales(assignments, null, http.RequestAborted);
        return Results.Json(new
        {
            Locale = await context.FindContentLocale(entityType, id, http.RequestAborted),
            EffectiveLocale = scope.EffectiveLocales.GetValueOrDefault(entityType + ":" + id)
        });
    }

    [IgnoreETag]
    public static async Task<IResult> Query(
        QueryRequest request, HttpContext http, AbstractCoreContext context,
        IAuthorizationService authorization, IEnumerable<IContentLocalePermission> permissions)
    {
        if (request.Items is null || request.Items.Count is < 1 or > 100
            || request.Items.Any(item => item is null || string.IsNullOrWhiteSpace(item.EntityType) || item.EntityType.Length > 200)
            || request.Items.Distinct().Count() != request.Items.Count)
        {
            return Results.BadRequest();
        }
        var statuses = new List<(QueryItem Item, int Status)>();
        foreach (var item in request.Items)
        {
            var error = await Check(item.EntityType, item.EntityId, http, context, authorization, permissions);
            statuses.Add((item, (error as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK));
        }
        var assignments = statuses.Any(item => item.Status == StatusCodes.Status200OK)
            ? await context.ContentLocales.LatestOf(context.ContentLocales)
                .Select(item => new ContentLocaleItem(item.EntityType, item.EntityId, item.Locale)).ToListAsync(http.RequestAborted)
            : [];
        var scope = assignments.Count > 0
            ? await context.ResolveContentLocales(assignments, null, http.RequestAborted)
            : new ContentLocaleScope([], new Dictionary<string, string?>());
        var explicitLocales = assignments.ToDictionary(item => item.EntityType + ":" + item.EntityId, item => item.Locale);
        return Results.Json(new
        {
            Items = statuses.Select(item => new QueryResult(item.Item.EntityType, item.Item.EntityId, item.Status,
                item.Status == StatusCodes.Status200OK
                    ? explicitLocales.GetValueOrDefault(item.Item.EntityType + ":" + item.Item.EntityId) : null,
                item.Status == StatusCodes.Status200OK
                    ? scope.EffectiveLocales.GetValueOrDefault(item.Item.EntityType + ":" + item.Item.EntityId) : null)).ToArray()
        });
    }

    [IgnoreETag]
    public static async Task<IResult> Save(
        SaveRequest request, HttpContext http, AbstractCoreContext context, LocaleCatalog locales,
        IAuthorizationService authorization, IEnumerable<IContentLocalePermission> permissions)
    {
        var error = await Check(request.EntityType, request.EntityId, http, context, authorization, permissions);
        if (error is not null)
        {
            return error;
        }

        var locale = await Normalize(request.Locale, locales, http.RequestAborted);
        if (request.Locale is not null && locale is null)
        {
            return Results.BadRequest();
        }

        var conflicts = request.EntityType switch
        {
            nameof(Site) => context.InContentLocale(context.Sites, nameof(Site), locale)
                .AnyAsync(candidate => EF.Property<Guid>(candidate, "SiteId") != request.EntityId
                    && context.Sites.Where(current => Microsoft.EntityFrameworkCore.EF.Property<Guid>(current, "SiteId") == request.EntityId)
                        .Any(current => current.Slug == candidate.Slug), http.RequestAborted),
            nameof(RouteAlias) => context.InContentLocale(context.RouteAliases.LatestOf(context.RouteAliases), nameof(RouteAlias), locale)
                .AnyAsync(candidate => EF.Property<Guid>(candidate, VersionedColumns.Key) != request.EntityId
                    && context.RouteAliases.OfKey(request.EntityId).LatestOf(context.RouteAliases).Any(current => current.Alias == candidate.Alias), http.RequestAborted),
            _ => context.HasContentLocaleSlugConflict(
                context.LocalizedContentTypes.Single(type => type.Name == request.EntityType),
                request.EntityId, locale, http.RequestAborted)
        };
        if (await conflicts)
        {
            return Results.Conflict();
        }

        await context.StageContentLocale(request.EntityType, request.EntityId, locale, http.RequestAborted);
        try
        {
            await context.SaveChangesAsync(http.RequestAborted);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return Results.Conflict();
        }

        return Results.NoContent();
    }

    public static async Task<string?> Normalize(string? locale, LocaleCatalog locales, CancellationToken cancellationToken) =>
        await locales.NormalizeContentLocale(locale, cancellationToken);

    internal static async Task<IResult?> Check(
        string entityType, Guid id, HttpContext http, AbstractCoreContext context,
        IAuthorizationService authorization, IEnumerable<IContentLocalePermission> permissions)
    {
        var type = context.LocalizedContentTypes.SingleOrDefault(type => type.Name == entityType);
        if (type is null || id == Guid.Empty)
        {
            return Results.BadRequest();
        }

        var policy = EditPolicy(context, entityType);
        var permission = permissions.FirstOrDefault(item => item.EntityType == entityType);
        var allowed = permission is not null
            ? await permission.CanEdit(http, id)
            : policy is not null && await authorization.HoldsGrantAsync(http.User, policy);
        if (!allowed)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await context.ContentItemExists(type, id, http.RequestAborted) ? null : Results.NotFound();
    }

    public static string? EditPolicy(AbstractCoreContext context, string entityType) =>
        CorePolicies.GetValueOrDefault(entityType) ?? context.Modules.Databases
            .Select(database => database.ContentLocaleEditPolicies.GetValueOrDefault(entityType))
            .FirstOrDefault(item => item is not null);

    extension(IEndpointRouteBuilder application)
    {
        public void MapContentLocaleEndpoints()
        {
            application.MapGet("/content-locales", List);
            application.MapGet("/content-locales/{entityType}/{id:guid}", Get).RequireAuthorization();
            application.MapPost("/content-locales", Save).RequireAuthorization()
                .AddEndpointFilter<DataAnnotationValidationFilter<SaveRequest>>();
            application.MapPost("/content-locales/query", Query).RequireAuthorization();
        }
    }
}
