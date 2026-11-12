using Behind.Common.WebApi;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Layouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public static class LayoutEndpoints
{
    public static async Task<IResult> Get(
        ILayoutService layouts, CancellationToken cancellationToken, HttpContext? context = null)
    {
        var configuration = await layouts.Read(cancellationToken);
        var locale = context?.Items[Middleware.ContentLocaleMiddleware.Header] as string;
        var visible = configuration.Layouts
            .Where(layout => !layout.Deleted && (layout.Locale == locale || layout.Locale is null)).ToArray();
        var ids = visible.Select(layout => layout.Id).ToHashSet(StringComparer.Ordinal);
        var fallback = visible.FirstOrDefault(layout => locale is not null && layout.Locale == locale)
            ?? visible.FirstOrDefault(layout => layout.Locale is null);
        return Results.Json(configuration with
        {
            Layouts = visible,
            DefaultLayout = ids.Contains(configuration.DefaultLayout) ? configuration.DefaultLayout : fallback?.Id ?? "",
            Assignments = configuration.Assignments.Where(assignment => ids.Contains(assignment.Layout)).ToArray()
        });
    }

    public static async Task<IResult> Manage(
        HttpContext context,
        IAuthorizationService authorization,
        ILayoutService layouts,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = ManagedListExtensions.NoStore;
        var configuration = await layouts.Read(cancellationToken);
        var deleted = await authorization.HoldsGrantAsync(context.User, ManagedListExtensions.DeletedRecordsGrant);
        return Results.Json(configuration with { Layouts = configuration.Layouts.Where(layout => deleted || !layout.Deleted).ToArray() });
    }

    public static async Task<IResult> Save(
        LayoutConfiguration request,
        HttpContext context,
        IAuthorizationService authorization,
        ILayoutService layouts,
        IOptions<SecurityConfiguration> security,
        ILogger<LayoutService> logger,
        IVfsCommands files,
        CancellationToken cancellationToken,
        [FromServices] LocaleCatalog? locales = null)
    {
        if (locales is not null)
        {
            var available = await locales.ListAsync(cancellationToken);
            if (request.Layouts.Any(layout => layout.Locale is not null
                && !available.Any(locale => string.Equals(locale.Code, layout.Locale, StringComparison.OrdinalIgnoreCase))))
            {
                return Results.BadRequest();
            }
            request = request with
            {
                Layouts = request.Layouts.Select(layout => layout with
                {
                    Locale = layout.Locale is null ? null : available.First(locale =>
                        string.Equals(locale.Code, layout.Locale, StringComparison.OrdinalIgnoreCase)).Code
                }).ToArray()
            };
        }
        if (security.Value.ReadOnlyConfiguration)
        {
            return Results.Json(ConfigurationForbiddenResponse.ConfigurationReadOnly, statusCode: StatusCodes.Status403Forbidden);
        }

        var deleted = await authorization.HoldsGrantAsync(context.User, ManagedListExtensions.DeletedRecordsGrant);
        var result = await layouts.Save(request, deleted, cancellationToken);

        if (result.Error is not null)
        {
            logger.LogWarning("Layout configuration save rejected: {Reason}", result.Error);
            return result.Error switch
            {
                "conflict" => Results.Conflict(),
                "forbidden" => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.BadRequest()
            };
        }

        await files.AttachSiteImagesOf(
            VfsTypes.Layouts,
            LayoutConfiguration.ImagesKey,
            result.Configuration!.Layouts.SelectMany(layout => layout.Sections)
                .SelectMany(section => section.Widgets)
                .Where(widget => widget.Type == "core.custom")
                .Select(widget => widget.Content),
            result.Configuration.Layouts.SelectMany(layout => layout.Badges)
                .Where(badge => badge.Image.Length > 0)
                .Select(badge => Guid.Parse(badge.Image[(VfsTypes.LayoutImageUrl.Length + 1)..])),
            cancellationToken);

        logger.LogInformation("Layout configuration saved at revision {Revision}", result.Configuration.Revision);
        return Results.Json(result.Configuration with
        {
            Layouts = result.Configuration.Layouts.Where(layout => deleted || !layout.Deleted).ToArray()
        });
    }
}
