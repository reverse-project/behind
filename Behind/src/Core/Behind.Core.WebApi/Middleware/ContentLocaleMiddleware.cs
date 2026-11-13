using Behind.Common.Database;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Layouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.WebApi.Middleware;

public sealed class ContentLocaleMiddleware(RequestDelegate next)
{
    public const string Header = "X-Content-Locale";

    public async Task InvokeAsync(
        HttpContext http, AbstractCoreContext context, LocaleCatalog locales, IAuthorizationService authorization,
        ILayoutService? layouts = null)
    {
        context.IncludeAllContentLocales = true;
        context.HiddenLocaleContentKeys = [];
        var requested = http.Request.Headers[Header];
        var query = http.Request.Query["contentLocale"];
        if (requested.Count > 1 || query.Count > 1
            || requested.Count > 0 && query.Count > 0 && requested.ToString() != query.ToString())
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var raw = requested.Count > 0 ? requested.ToString() : query.Count > 0 ? query.ToString() : null;
        var available = await locales.ListAsync(http.RequestAborted);
        var selected = raw is null ? null : available.FirstOrDefault(item =>
            string.Equals(item.Code, raw, StringComparison.OrdinalIgnoreCase))?.Code;
        if (raw is not null && selected is null)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        context.SelectedContentLocale = selected;
        http.Items[Header] = selected;
        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers.AppendCommaSeparatedValues("Vary", Header);
        http.Response.OnStarting(() =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.Headers.Remove("ETag");
            http.Response.Headers.Remove("Last-Modified");
            return Task.CompletedTask;
        });

        if (await IsManagement(http, authorization))
        {
            await next(http);
            if (!http.Response.HasStarted)
            {
                http.Response.Headers.CacheControl = "private, no-store";
            }
            return;
        }

        var assignments = await context.ContentLocales.LatestOf(context.ContentLocales)
            .Select(item => new ContentLocaleItem(item.EntityType, item.EntityId, item.Locale))
            .ToListAsync(http.RequestAborted);
        if (layouts is not null)
        {
            var configuration = await layouts.Read(http.RequestAborted);
            var media = configuration.Layouts.Where(layout => !layout.Deleted)
                .SelectMany(layout => layout.ImageKeys.Select(id => new ContentLocaleItem(nameof(VfsFile), id, layout.Locale)))
                .GroupBy(item => item.EntityId)
                .Select(group => group.FirstOrDefault(item => item.Locale == selected)
                    ?? group.FirstOrDefault(item => item.Locale is null)
                    ?? group.First())
                .Where(item => !assignments.Any(assignment => assignment.EntityType == item.EntityType
                    && assignment.EntityId == item.EntityId));
            assignments.AddRange(media);
        }
        context.HiddenLocaleContentKeys = await context.HiddenForContentLocale(assignments, selected, http.RequestAborted);
        var globalImages = await context.VfsFiles.Where(file => file.Type == UserProfiles.FileType
                || file.Type == VfsTypes.AccessSettings)
            .Select(file => EF.Property<Guid>(file, VersionedColumns.Key)).Distinct().ToListAsync(http.RequestAborted);
        context.HiddenLocaleContentKeys = context.HiddenLocaleContentKeys
            .Except(globalImages.Select(id => nameof(VfsFile) + ":" + id)).ToArray();
        context.IncludeAllContentLocales = false;
        await next(http);
        if (!http.Response.HasStarted)
        {
            http.Response.Headers.CacheControl = "private, no-store";
        }
    }

    public static async Task<bool> IsManagement(HttpContext http, IAuthorizationService authorization)
    {
        var endpoint = http.GetEndpoint();
        var policies = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Where(item => !string.IsNullOrEmpty(item.Policy)).Select(item => item.Policy!).Distinct().ToArray() ?? [];
        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "";
        if (route is "/content-locales/{entityType}/{id:guid}" or "/content-locales" or "/content-locales/query"
            or "/content-restrictions/{entityType}/{id:guid}" or "/content-restrictions")
        {
            return true;
        }
        if (policies.Length == 0)
        {
            return false;
        }

        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var management = segments.Contains("manage") || segments.Contains("versions")
            || segments.Contains("raw-database") || segments.Contains("content-locales")
            || segments.Contains("content-restrictions")
            || route is "/sites" or "/sites/{id:guid}" or "/sites/{siteId:guid}/carousel"
                or "/messages/{id:guid}" or "/layouts/manage" or "/downloads/deleted"
            || !HttpMethods.IsGet(http.Request.Method) && policies.Any(policy =>
                policy.EndsWith(".Write", StringComparison.Ordinal) || policy.EndsWith(".Manage", StringComparison.Ordinal)
                || policy.EndsWith(".Delete", StringComparison.Ordinal) || policy == "ForumModeration.Manage")
            || !HttpMethods.IsGet(http.Request.Method) && route is "/forum/posts/{id:guid}"
                or "/forum/posts/{id:guid}/delete" or "/widgets/chat/{id:guid}";
        if (!management)
        {
            return false;
        }

        foreach (var policy in policies)
        {
            if (!(await authorization.AuthorizeAsync(http.User, null, policy)).Succeeded)
            {
                return false;
            }
        }

        return true;
    }
}
