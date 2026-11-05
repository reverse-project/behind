using System.ComponentModel.DataAnnotations;
using static Behind.Common.FunctionalExtensions;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Captcha;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.MagicLinks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignInEndpoints
{
    private const string NoStore = "no-store";

    public sealed record PasswordRequest(
        [property: Required, DataType(DataType.EmailAddress)]
        string Email,
        [property: Required] string Password,
        CaptchaResponse? Captcha = null) : ICaptchaRequest;

    public sealed record BannedResponse(string Reason);

    public sealed record EmailNotConfirmedResponse(string Code = EmailNotConfirmedCode);

    public const string EmailNotConfirmedCode = "emailNotConfirmed";

    public sealed record SignInResponse(
        string AccessToken,
        DateTimeOffset Expires,
        string RefreshToken,
        MagicLinkFunction? Function = null);

    public sealed record MagicLinkRequest(
        [property: Required, DataType(DataType.EmailAddress)]
        string Email,
        MagicLinkFunction Function = MagicLinkFunction.SignIn,
        CaptchaResponse? Captcha = null) : ICaptchaRequest;

    public sealed record MagicLinkSignInRequest([property: Required] string Token);

    [IgnoreETag]
    public static async Task<IResult> SignInPassword(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, ISessionCommands sessionCommands,
        IWallOfShameQueries wallOfShameQueries, TimeProvider timeProvider,
        HttpContext httpContext, ILogger<SignInEndpoints> logger, PasswordRequest request)
    {
        return await securityConfiguration.Value.Jwt.Match(
            none: () => NotConfigured(logger, nameof(SignInPassword)),
            async jwtSettings =>
            {
                var user = await userManager.FindByEmailAsync(request.Email);
                if (user is null || user.Password?.Verify(request.Password) != true)
                {
                    logger.LogWarning(
                        "Password sign-in rejected for user {UserId}: {Reason}",
                        () => [user?.Id, user is null ? "unknown email" : "wrong password"]);

                    return Results.Unauthorized();
                }

                return await StartSession(
                    user, jwtSettings, securityConfiguration.Value.EmailConfirmation, sessionCommands,
                    wallOfShameQueries, timeProvider.GetUtcNow(), httpContext, logger);
            });
    }

    [IgnoreETag]
    public static async Task<IResult> RequestMagicLink(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, IMagicLinkSettingsStore settingsStore,
        IMagicLinkCommands magicLinkCommands, IMagicLinkQueries magicLinkQueries, IMagicLinkMailQueue mailQueue,
        IConfiguration configuration, TimeProvider timeProvider, HttpContext httpContext,
        ILogger<SignInEndpoints> logger, MagicLinkRequest request)
    {
        return await securityConfiguration.Value.Jwt.Match(
            none: () => NotConfigured(logger, nameof(RequestMagicLink)),
            jwtSettings => settingsStore.Current.Match(
                none: () =>
                {
                    logger.LogWarning("Magic link request rejected: magic links are not configured");

                    return Task.FromResult(Results.NotFound());
                },
                async settings =>
                {
                    var user = await userManager.FindByEmailAsync(request.Email);
                    var now = timeProvider.GetUtcNow();

                    if (user is null)
                    {
                        logger.LogDebug("Magic link requested for an unknown email, no link issued");
                    }
                    else if (await magicLinkQueries.HasIssuedSince(
                                 user.Id,
                                 now - settings.EffectiveRequestCooldown,
                                 httpContext.RequestAborted))
                    {
                        logger.LogDebug(
                            "Magic link for user {UserId} not issued: another link was issued within the last {Cooldown}",
                            () => [user.Id, settings.EffectiveRequestCooldown]);
                    }
                    else
                    {
                        var magicLink = MagicLink.Create(now, settings.EffectiveLinkLifetime, request.Function);
                        await magicLinkCommands.Issue(user.Id, magicLink, httpContext.RequestAborted);

                        var link = MagicLinkCredential.Create(magicLink.Nonce, settings.ActualSecret, jwtSettings.SigningKey)
                            .ToLink(configuration.FrontendUri().GetOrElse(() => httpContext.BaseUri()));

                        var queued = mailQueue.TryEnqueue(new MagicLinkMail(user, request.Email, link, request.Function));

                        logger.Log(
                            queued ? LogLevel.Information : LogLevel.Warning,
                            "Magic link ({Function}) for user {UserId} issued, expires at {ExpiresAt}, email queued: {Queued}",
                            () => [request.Function, user.Id, magicLink.ExpiresAt, queued]);
                    }

                    return Results.NoContent();
                }));
    }

    [IgnoreETag]
    public static async Task<IResult> SignInMagicLink(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, IMagicLinkSettingsStore settingsStore,
        IMagicLinkCommands magicLinkCommands, ISessionCommands sessionCommands,
        IWallOfShameQueries wallOfShameQueries, TimeProvider timeProvider,
        HttpContext httpContext, ILogger<SignInEndpoints> logger, MagicLinkSignInRequest request)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        return await securityConfiguration.Value.Jwt.Match(
            none: () => NotConfigured(logger, nameof(SignInMagicLink)),
            jwtSettings => settingsStore.Current.Match(
                none: () =>
                {
                    logger.LogWarning("Magic link redemption rejected: magic links are not configured");

                    return Task.FromResult(Results.NotFound());
                },
                async settings =>
                {
                    var now = timeProvider.GetUtcNow();
                    var credential = MagicLinkCredential.TryParse(request.Token)
                        .Where(c => c.IsSignedBy(settings.ValidSecrets(now), jwtSettings.SigningKey));

                    var redemption = await credential.Match(
                        none: () =>
                        {
                            logger.LogWarning("Magic link redemption rejected: credential is malformed or not signed");

                            return Task.FromResult<Option<MagicLinkRedemption>>(None);
                        },
                        some: async c =>
                        {
                            var consumed = await magicLinkCommands.Consume(c.Nonce, now, httpContext.RequestAborted);

                            if (consumed == None)
                            {
                                logger.LogWarning(
                                    "Magic link redemption rejected: link is unknown, expired or already used");
                            }

                            return consumed;
                        });

                    var user = await redemption.Match(
                        none: () => Task.FromResult<ApplicationUser?>(null),
                        some: async r =>
                        {
                            var found = await userManager.FindByIdAsync(r.UserId.ToString());

                            if (found is null)
                            {
                                logger.LogWarning(
                                    "Magic link redemption rejected: user {UserId} no longer exists",
                                    () => [r.UserId]);
                            }

                            return found;
                        });

                    return user is null
                        ? Results.Unauthorized()
                        : await StartSession(
                            user, jwtSettings, securityConfiguration.Value.EmailConfirmation, sessionCommands,
                            wallOfShameQueries, now, httpContext, logger,
                            redemption.Match(() => (MagicLinkFunction?)null, r => r.Function));
                }));
    }

    private static Task<IResult> NotConfigured(ILogger logger, string operation)
    {
        logger.LogError("{Operation} rejected: JWT settings are not configured", () => [operation]);

        return Task.FromResult<IResult>(Results.InternalServerError());
    }

    private static async Task<IResult> StartSession(ApplicationUser user, JwtSettings jwtSettings,
        bool emailConfirmationRequired, ISessionCommands sessionCommands, IWallOfShameQueries wallOfShameQueries,
        DateTimeOffset now, HttpContext httpContext, ILogger logger, MagicLinkFunction? function = null)
    {
        if (emailConfirmationRequired && !user.HasConfirmedEmail)
        {
            logger.LogWarning("Sign-in refused for user {UserId}: the email is not confirmed", () => [user.Id]);

            return Results.Json(new EmailNotConfirmedResponse(), statusCode: StatusCodes.Status403Forbidden);
        }

        var ban = await wallOfShameQueries.FindActiveBan(user.Id, now, httpContext.RequestAborted);

        return await ban.Match(
            none: () => StartPermittedSession(user, jwtSettings, sessionCommands, httpContext, logger, function),
            some: active =>
            {
                logger.LogWarning("Sign-in refused for banned user {UserId}", () => [user.Id]);

                return Task.FromResult(Results.Json(
                    new BannedResponse(active.Reason),
                    statusCode: StatusCodes.Status403Forbidden));
            });
    }

    private static async Task<IResult> StartPermittedSession(ApplicationUser user, JwtSettings jwtSettings,
        ISessionCommands sessionCommands, HttpContext httpContext, ILogger logger, MagicLinkFunction? function)
    {
        var humanUser = user.ToHumanUser();
        var accessToken = AccessToken.Create(humanUser);
        var refreshToken = RefreshToken.Create(humanUser);

        var sessionId = await sessionCommands.CreateForUserId(
            user.Id,
            new ValidSession(
                accessToken,
                refreshToken,
                httpContext.ClientAddress(RemoteAddress.MaxLength),
                httpContext.ClientUserAgent(UserAgent.MaxLength)),
            httpContext.RequestAborted);

        logger.LogInformation(
            "Session {SessionId} started for user {UserId}, access token expires at {AccessTokenExpiresAt}",
            () => [sessionId, user.Id, accessToken.ExpiresAt]);

        return Results.Json(user.ToSignInResponse(accessToken, refreshToken, sessionId, jwtSettings) with
        {
            Function = function
        });
    }
}
