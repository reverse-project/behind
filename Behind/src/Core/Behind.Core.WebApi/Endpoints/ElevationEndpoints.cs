using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using static Behind.Common.FunctionalExtensions;
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
        ILogger<ElevationEndpoints> logger,
        IMagicLinkCommands magicLinks) =>
        ElevateTo(
            BuiltInAccess.Administrators,
            securityConfiguration.Value.SecretKey,
            request,
            httpContext,
            securityConfiguration,
            sessionQueries,
            roleCommands,
            timeProvider,
            logger,
            magicLinks);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> ElevateDeveloper(
        ElevationRequest request,
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        IRoleCommands roleCommands,
        TimeProvider timeProvider,
        ILogger<ElevationEndpoints> logger,
        IMagicLinkCommands magicLinks) =>
        ElevateTo(
            BuiltInAccess.Developers,
            securityConfiguration.Value.DeveloperElevationSecretKey,
            request,
            httpContext,
            securityConfiguration,
            sessionQueries,
            roleCommands,
            timeProvider,
            logger,
            magicLinks);

    private static async Task<IResult> ElevateTo(
        BuiltInRole role,
        Option<SecretKey> secretKey,
        ElevationRequest request,
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        IRoleCommands roleCommands,
        TimeProvider timeProvider,
        ILogger logger,
        IMagicLinkCommands magicLinks)
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
                var now = timeProvider.GetUtcNow();
                var proof = secretKey.Bind(key => key is Totp totp
                    ? totp.VerifiedStep(request.Code, now).Map(step =>
                        new TokenNonce(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                            FormattableString.Invariant(
                                $"elevation:{OtpNet.Base32Encoding.ToString(OtpNet.Base32Encoding.ToBytes(totp.Value))}:{step}")))[..16])))
                    : None);
                var accepted = await proof.Match(
                    () => Task.FromResult(false),
                    nonce => magicLinks.TryIssue(session.UserId,
                        new MagicLink(nonce, now, DateTimeOffset.MaxValue, MagicLinkFunction.TwoFactorChallenge, Some(now)),
                        httpContext.RequestAborted));

                if (!accepted)
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
