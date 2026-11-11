using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Layouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public static class LayoutEndpoints
{
    public static async Task<IResult> Get(ILayoutService layouts, CancellationToken cancellationToken)
    {
        var configuration = await layouts.Read(cancellationToken);
        return Results.Json(configuration with { Layouts = configuration.Layouts.Where(layout => !layout.Deleted).ToArray() });
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
        CancellationToken cancellationToken)
    {
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

        logger.LogInformation("Layout configuration saved at revision {Revision}", result.Configuration!.Revision);
        return Results.Json(result.Configuration with
        {
            Layouts = result.Configuration.Layouts.Where(layout => deleted || !layout.Deleted).ToArray()
        });
    }
}
