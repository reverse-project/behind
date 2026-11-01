using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class ElevationEndpoints
{
    public sealed record ElevationRequest([Required, RegularExpression(@"^\d{6,8}$")] string Code);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> ElevateAdministrator(
        ElevationRequest request,
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        IRoleCommands roleCommands,
        TimeProvider timeProvider) =>
        ElevateTo(
            BuiltInAccess.Administrators,
            securityConfiguration.Value.SecretKey,
            request,
            httpContext,
            securityConfiguration,
            sessionQueries,
            roleCommands,
            timeProvider);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> ElevateDeveloper(
        ElevationRequest request,
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        IRoleCommands roleCommands,
        TimeProvider timeProvider) =>
        ElevateTo(
            BuiltInAccess.Developers,
            securityConfiguration.Value.DeveloperElevationSecretKey,
            request,
            httpContext,
            securityConfiguration,
            sessionQueries,
            roleCommands,
            timeProvider);

    private static async Task<IResult> ElevateTo(
        BuiltInRole role,
        Option<SecretKey> secretKey,
        ElevationRequest request,
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        IRoleCommands roleCommands,
        TimeProvider timeProvider)
    {
        if (!securityConfiguration.Value.IsElevationEnabled)
        {
            return Results.NotFound();
        }

        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                if (!secretKey.VerifyTotp(request.Code, timeProvider.GetUtcNow()))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                await roleCommands.AssignBuiltIn(session.UserId, role, httpContext.RequestAborted);

                return Results.NoContent();
            });
    }
}
