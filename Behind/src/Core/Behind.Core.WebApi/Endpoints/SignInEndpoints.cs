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
using Behind.Core.WebApi.TwoFactor;
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

    public sealed record ApprovalPendingResponse(string Code = ApprovalPendingCode);

    public const string ApprovalPendingCode = "approvalPending";

    public sealed record SignInResponse(
        string AccessToken,
        DateTimeOffset Expires,
        string RefreshToken,
        MagicLinkFunction? Function = null,
        IReadOnlyList<string>? RecoveryCodes = null);

    public sealed record MagicLinkRequest(
        [property: Required, DataType(DataType.EmailAddress)]
        string Email,
        [property: AllowedValues(MagicLinkFunction.SignIn, MagicLinkFunction.LostPassword)]
        MagicLinkFunction Function = MagicLinkFunction.SignIn,
        CaptchaResponse? Captcha = null) : ICaptchaRequest;

    public sealed record MagicLinkSignInRequest([property: Required] string Token);

    [IgnoreETag]
    public static async Task<IResult> SignInPassword(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, ISessionCommands sessionCommands,
        IWallOfShameQueries wallOfShameQueries, ITwoFactorGate twoFactorGate, TimeProvider timeProvider,
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

                if (securityConfiguration.Value.EmailConfirmation && !ConfirmedAddress(user, request.Email))
                {
                    logger.LogWarning("Sign-in refused for user {UserId}: the requested email is not confirmed", () => [user.Id]);
                    return Results.Json(new EmailNotConfirmedResponse(), statusCode: StatusCodes.Status403Forbidden);
                }

                return await StartSession(
                    user, jwtSettings, securityConfiguration.Value.EmailConfirmation, sessionCommands,
                    wallOfShameQueries, timeProvider.GetUtcNow(), httpContext, logger, Some(twoFactorGate));
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
                    if (request.Function is not (MagicLinkFunction.SignIn or MagicLinkFunction.LostPassword))
                    {
                        logger.LogWarning("Magic link request rejected: {Function} cannot be requested", () => [request.Function]);

                        return Results.BadRequest();
                    }

                    var frontend = configuration.FrontendUri().Match<Uri?>(() => null, uri => uri);
                    if (frontend is null)
                    {
                        logger.LogError("Magic link request rejected: Frontend:Url must be an absolute HTTP(S) URL without credentials, query or fragment");
                        return Results.InternalServerError();
                    }

                    var user = await userManager.FindByEmailAsync(request.Email);
                    var now = timeProvider.GetUtcNow();

                    if (user is null)
                    {
                        logger.LogDebug("Magic link requested for an unknown email, no link issued");
                    }
                    else if (securityConfiguration.Value.EmailConfirmation && !ConfirmedAddress(user, request.Email))
                    {
                        logger.LogWarning("Magic link for user {UserId} not issued: the requested email is not confirmed", () => [user.Id]);
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
                            .ToLink(frontend);

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
        IWallOfShameQueries wallOfShameQueries, ITwoFactorGate twoFactorGate,
        IPasswordResetGrants passwordResetGrants, TimeProvider timeProvider,
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
                            var consumed = (await magicLinkCommands.Consume(c.Nonce, now, httpContext.RequestAborted))
                                .Where(r => r.Function is MagicLinkFunction.SignIn or MagicLinkFunction.LostPassword);

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
                            wallOfShameQueries, now, httpContext, logger, Some(twoFactorGate),
                            redemption.Match(() => (MagicLinkFunction?)null, r => r.Function),
                            passwordResetGrants: passwordResetGrants);
                }));
    }

    private static bool ConfirmedAddress(ApplicationUser user, string value) =>
        user.Emails.Any(email => string.Equals(email.Value, value.Trim(), StringComparison.OrdinalIgnoreCase)
            && email.ConfirmedAt is not null);

    internal static Task<IResult> NotConfigured(ILogger logger, string operation)
    {
        logger.LogError("{Operation} rejected: JWT settings are not configured", () => [operation]);

        return Task.FromResult<IResult>(Results.InternalServerError());
    }

    internal static async Task<IResult> StartSession(ApplicationUser user, JwtSettings jwtSettings,
        bool emailConfirmationRequired, ISessionCommands sessionCommands, IWallOfShameQueries wallOfShameQueries,
        DateTimeOffset now, HttpContext httpContext, ILogger logger, Option<ITwoFactorGate> secondFactor,
        MagicLinkFunction? function = null, IReadOnlyList<string>? recoveryCodes = null,
        IPasswordResetGrants? passwordResetGrants = null,
        string? primaryCredentialId = null)
    {
        if (emailConfirmationRequired && !user.HasConfirmedEmail)
        {
            logger.LogWarning("Sign-in refused for user {UserId}: the email is not confirmed", () => [user.Id]);

            return Results.Json(new EmailNotConfirmedResponse(), statusCode: StatusCodes.Status403Forbidden);
        }

        if (user.PendingApproval)
        {
            logger.LogWarning("Sign-in refused for user {UserId}: the account is waiting for approval", () => [user.Id]);

            return Results.Json(new ApprovalPendingResponse(), statusCode: StatusCodes.Status403Forbidden);
        }

        var ban = await wallOfShameQueries.FindActiveBan(user.Id, now, httpContext.RequestAborted);

        return await ban.Match(
            none: async () =>
            {
                var challenge = await secondFactor.Match(
                    none: () => Task.FromResult<Option<TwoFactorChallengeResponse>>(None),
                    gate => primaryCredentialId is null
                        ? gate.Challenge(user.Id, function, httpContext.RequestAborted)
                        : gate.ChallengePasskey(user.Id, primaryCredentialId, httpContext.RequestAborted));

                return await challenge.Match(
                    none: () => function switch
                    {
                        null or MagicLinkFunction.SignIn => StartPermittedSession(
                            user, jwtSettings, sessionCommands, httpContext, logger, function, recoveryCodes),
                        MagicLinkFunction.LostPassword => StartPasswordReset(
                            user, passwordResetGrants, httpContext, logger, recoveryCodes),
                        _ => Task.FromResult<IResult>(Results.Unauthorized())
                    },
                    pending =>
                    {
                        if (pending.Unavailable)
                        {
                            logger.LogWarning("Sign-in refused for user {UserId}: an authentication method is disabled", () => [user.Id]);
                            return Task.FromResult<IResult>(Results.Json(
                                new TwoFactorFailure(TwoFactorFailure.Unavailable),
                                statusCode: StatusCodes.Status403Forbidden));
                        }

                        logger.LogInformation(
                            "Second factor required for user {UserId}, setup required: {SetupRequired}",
                            () => [user.Id, pending.SetupRequired]);

                        httpContext.Response.Headers.CacheControl = NoStore;

                        return Task.FromResult(Results.Json(pending));
                    });
            },
            some: active =>
            {
                logger.LogWarning("Sign-in refused for banned user {UserId}", () => [user.Id]);

                return Task.FromResult(Results.Json(
                    new BannedResponse(active.Reason),
                    statusCode: StatusCodes.Status403Forbidden));
            });
    }

    private static async Task<IResult> StartPasswordReset(ApplicationUser user,
        IPasswordResetGrants? passwordResetGrants, HttpContext httpContext, ILogger logger,
        IReadOnlyList<string>? recoveryCodes)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        if (passwordResetGrants is null)
        {
            logger.LogError("Password reset for user {UserId} rejected: no password reset grants available", () => [user.Id]);

            return Results.InternalServerError();
        }

        var grant = await passwordResetGrants.Issue(user.Id, httpContext.RequestAborted);

        return grant.Match<IResult>(
            none: () =>
            {
                logger.LogError("Password reset for user {UserId} rejected: magic links are not configured", () => [user.Id]);

                return Results.InternalServerError();
            },
            some: issued =>
            {
                logger.LogInformation(
                    "Password reset grant issued for user {UserId} instead of a session, expires at {ExpiresAt}",
                    () => [user.Id, issued.ExpiresAt]);

                return Results.Json(new PasswordResetEndpoints.PasswordResetResponse(
                    issued.Token, issued.ExpiresAt, RecoveryCodes: recoveryCodes));
            });
    }

    private static async Task<IResult> StartPermittedSession(ApplicationUser user, JwtSettings jwtSettings,
        ISessionCommands sessionCommands, HttpContext httpContext, ILogger logger, MagicLinkFunction? function,
        IReadOnlyList<string>? recoveryCodes)
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
            Function = function,
            RecoveryCodes = recoveryCodes
        });
    }
}
