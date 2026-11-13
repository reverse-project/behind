using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class TwoFactorProfileEndpoints
{
    public sealed record PasskeyResponse(Guid Id, string Name, DateTimeOffset? CreatedAt);

    public sealed record StatusResponse(
        bool Required,
        bool Enabled,
        bool TotpEnabled,
        bool TotpPending,
        IReadOnlyList<PasskeyResponse> Passkeys,
        int RecoveryCodesRemaining);

    public sealed record CodeRequest([property: Required] string Code);

    public sealed record PasskeyRegistrationRequest(
        [property: Required] string Ticket,
        [property: Required] AuthenticatorAttestationRawResponse Credential,
        [property: MaxLength(UserPasskey.MaxNameLength)] string? Name = null);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> GetStatus(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ITwoFactorQueries queries,
        IOptions<SecurityConfiguration> security,
        ILogger<TwoFactorProfileEndpoints> logger) =>
        ForSession(httpContext, sessionQueries, userManager, logger, async user =>
        {
            var status = await queries.GetStatus(user.Id, httpContext.RequestAborted);
            var pending = !status.TotpEnabled
                          && await queries.FindTotp(user.Id, httpContext.RequestAborted) != None;

            return Results.Json(new StatusResponse(
                security.Value.TwoFactorRequired,
                status.IsEnabled,
                status.TotpEnabled,
                pending,
                status.Passkeys
                    .Select(passkey => new PasskeyResponse(passkey.Id, passkey.Name, passkey.CreatedAt.ToNullable()))
                    .ToList(),
                status.RecoveryCodesRemaining));
        });

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> BeginTotp(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorProfileEndpoints> logger) =>
        ForSession(httpContext, sessionQueries, userManager, logger, async user =>
            (await enrollment.BeginTotp(user, httpContext.RequestAborted)).Match<IResult>(
                () => Results.Conflict(new TwoFactorFailure(TwoFactorFailure.AlreadyEnabled)),
                setup => Results.Json(setup)));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> ConfirmTotp(
        CodeRequest request,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorProfileEndpoints> logger) =>
        ForSession(httpContext, sessionQueries, userManager, logger, async user =>
            Enrolled(
                logger,
                user,
                "authenticator app",
                await enrollment.ConfirmTotp(user.Id, request.Code, httpContext.RequestAborted)));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> RemoveTotp(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorProfileEndpoints> logger) =>
        ForSession(httpContext, sessionQueries, userManager, logger, async user =>
            Removed(logger, user, "authenticator app", await enrollment.RemoveTotp(user.Id, httpContext.RequestAborted)));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> PasskeyOptions(
        HttpContext httpContext,
        IConfiguration configuration,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorProfileEndpoints> logger) =>
        ForSession(httpContext, sessionQueries, userManager, logger, async user =>
            Results.Json(await enrollment.BeginPasskey(
                configuration.PasskeyOrigin(),
                user,
                TwoFactorStage.Profile,
                null,
                httpContext.RequestAborted)));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> AddPasskey(
        PasskeyRegistrationRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ITwoFactorTickets tickets,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorProfileEndpoints> logger) =>
        ForSession(httpContext, sessionQueries, userManager, logger, async user =>
        {
            var ticket = tickets.Open(request.Ticket)
                .Where(opened => opened.Stage == TwoFactorStage.Profile && opened.UserId == user.Id);

            return await ticket.Match(
                () => Task.FromResult(TwoFactorSignInEndpoints.TicketRejected()),
                async opened => Enrolled(
                    logger,
                    user,
                    "passkey",
                    await enrollment.CompletePasskey(
                        configuration.PasskeyOrigin(),
                        opened,
                        request.Name,
                        request.Credential,
                        httpContext.RequestAborted)));
        });

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> RemovePasskey(
        Guid id,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorProfileEndpoints> logger) =>
        ForSession(httpContext, sessionQueries, userManager, logger, async user =>
            Removed(logger, user, "passkey", await enrollment.RemovePasskey(user.Id, id, httpContext.RequestAborted)));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> RegenerateRecoveryCodes(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ITwoFactorQueries queries,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorProfileEndpoints> logger) =>
        ForSession(httpContext, sessionQueries, userManager, logger, async user =>
        {
            if (!(await queries.GetStatus(user.Id, httpContext.RequestAborted)).IsEnabled)
            {
                return Results.Conflict(new TwoFactorFailure(TwoFactorFailure.NotEnabled));
            }

            var codes = await enrollment.RegenerateRecoveryCodes(user.Id, httpContext.RequestAborted);

            logger.LogInformation("User {UserId} regenerated the recovery codes", () => [user.Id]);

            return Results.Json(new Enrolled(codes));
        });

    private static async Task<IResult> ForSession(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        Func<ApplicationUser, Task<IResult>> action)
    {
        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                if (session.IsImpersonated)
                {
                    logger.LogWarning(
                        "Two-factor change rejected: session {SessionId} is an impersonation session",
                        () => [session.SessionId]);

                    return Results.Forbid();
                }

                var user = await userManager.FindByIdAsync(session.UserId.ToString());

                return user is null ? Results.Unauthorized() : await action(user);
            });
    }

    private static IResult Enrolled(ILogger logger, ApplicationUser user, string method, Option<Enrolled> enrolled) =>
        enrolled.Match<IResult>(
            () => Results.Json(
                new TwoFactorFailure(TwoFactorFailure.InvalidCode),
                statusCode: StatusCodes.Status403Forbidden),
            done =>
            {
                logger.LogInformation(
                    "User {UserId} enabled {Method}, recovery codes issued: {Issued}",
                    () => [user.Id, method, done.RecoveryCodes.Count > 0]);

                return Results.Json(done);
            });

    private static IResult Removed(ILogger logger, ApplicationUser user, string method, TwoFactorRemoval removal)
    {
        logger.LogInformation(
            "User {UserId} removal of {Method}: {Removal}",
            () => [user.Id, method, removal]);

        return removal switch
        {
            TwoFactorRemoval.Removed => Results.NoContent(),
            TwoFactorRemoval.NotFound => Results.NotFound(),
            _ => Results.Conflict(new TwoFactorFailure(TwoFactorFailure.Required))
        };
    }
}
