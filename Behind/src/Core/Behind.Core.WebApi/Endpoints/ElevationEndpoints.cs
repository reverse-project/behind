using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class ElevationEndpoints
{
    public sealed record ElevationRequest([property: Required, RegularExpression(@"^\d{6,8}$")] string Code);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> ElevateAdministrator(
        ElevationRequest request,
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        IRoleCommands roleCommands,
        TimeProvider timeProvider,
        ILogger<ElevationEndpoints> logger) =>
        ElevateTo(
            BuiltInAccess.Administrators,
            securityConfiguration.Value.SecretKey,
            request,
            httpContext,
            securityConfiguration,
            sessionQueries,
            roleCommands,
            timeProvider,
            logger);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> ElevateDeveloper(
        ElevationRequest request,
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        IRoleCommands roleCommands,
        TimeProvider timeProvider,
        ILogger<ElevationEndpoints> logger) =>
        ElevateTo(
            BuiltInAccess.Developers,
            securityConfiguration.Value.DeveloperElevationSecretKey,
            request,
            httpContext,
            securityConfiguration,
            sessionQueries,
            roleCommands,
            timeProvider,
            logger);

    private static async Task<IResult> ElevateTo(
        BuiltInRole role,
        Option<SecretKey> secretKey,
        ElevationRequest request,
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        IRoleCommands roleCommands,
        TimeProvider timeProvider,
        ILogger logger)
    {
        if (!securityConfiguration.Value.IsElevationEnabled)
        {
            logger.LogWarning("Elevation to {Role} rejected: elevation is not enabled", () => [role.Role.Name]);

            return Results.NotFound();
        }

        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () =>
            {
                logger.LogWarning("Elevation to {Role} rejected: no active session", () => [role.Role.Name]);

                return Task.FromResult<IResult>(Results.Unauthorized());
            },
            async session =>
            {
                if (!secretKey.VerifyTotp(request.Code, timeProvider.GetUtcNow()))
                {
                    logger.LogWarning(
                        "Elevation of user {UserId} to {Role} rejected: invalid one-time code",
                        () => [session.UserId, role.Role.Name]);

                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                await roleCommands.AssignBuiltIn(session.UserId, role, httpContext.RequestAborted);

                logger.LogInformation("User {UserId} elevated to {Role}", () => [session.UserId, role.Role.Name]);

                return Results.NoContent();
            });
    }
}
