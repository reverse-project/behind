using System.ComponentModel.DataAnnotations;
using static Behind.Common.FunctionalExtensions;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Behind.Common;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.MagicLinks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignInEndpoints
{
    private const string NoStore = "no-store";

    public sealed record PasswordRequest(
        [property: Required, DataType(DataType.EmailAddress)]
        string Email,
        [property: Required] string Password);

    public sealed record BannedResponse(string Reason);

    public sealed record SignInResponse(string AccessToken, DateTimeOffset Expires, string RefreshToken);

    public sealed record MagicLinkRequest(
        [property: Required, DataType(DataType.EmailAddress)]
        string Email);

    public sealed record MagicLinkSignInRequest([property: Required] string Token);

    [IgnoreETag]
    public static async Task<IResult> SignInPassword(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, ISessionCommands sessionCommands,
        IWallOfShameQueries wallOfShameQueries, TimeProvider timeProvider,
        HttpContext httpContext, PasswordRequest request)
    {
        return await securityConfiguration.Value.Jwt.Match(
            none: () => Task.FromResult(Results.InternalServerError()),
            async jwtSettings =>
            {
                var user = await userManager.FindByEmailAsync(request.Email);
                if (user is null || user.Password?.Verify(request.Password) != true)
                {
                    return Results.Unauthorized();
                }

                return await StartSession(
                    user, jwtSettings, sessionCommands, wallOfShameQueries, timeProvider.GetUtcNow(), httpContext);
            });
    }

    [IgnoreETag]
    public static async Task<IResult> RequestMagicLink(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, IMagicLinkSettingsStore settingsStore,
        IMagicLinkCommands magicLinkCommands, IMagicLinkQueries magicLinkQueries, IMagicLinkMailQueue mailQueue,
        IConfiguration configuration, TimeProvider timeProvider, HttpContext httpContext, MagicLinkRequest request)
    {
        return await securityConfiguration.Value.Jwt.Match(
            none: () => Task.FromResult(Results.InternalServerError()),
            jwtSettings => settingsStore.Current.Match(
                none: () => Task.FromResult(Results.NotFound()),
                async settings =>
                {
                    var user = await userManager.FindByEmailAsync(request.Email);
                    var now = timeProvider.GetUtcNow();

                    if (user is not null && !await magicLinkQueries.HasIssuedSince(
                            user.Id,
                            now - settings.EffectiveRequestCooldown,
                            httpContext.RequestAborted))
                    {
                        var magicLink = MagicLink.Create(now, settings.EffectiveLinkLifetime);
                        await magicLinkCommands.Issue(user.Id, magicLink, httpContext.RequestAborted);

                        var link = MagicLinkCredential.Create(magicLink.Nonce, settings.ActualSecret, jwtSettings.SigningKey)
                            .ToLink(FrontendUri(configuration).GetOrElse(() => httpContext.BaseUri()));

                        mailQueue.TryEnqueue(new MagicLinkMail(user, request.Email, link));
                    }

                    return Results.NoContent();
                }));
    }

    [IgnoreETag]
    public static async Task<IResult> SignInMagicLink(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, IMagicLinkSettingsStore settingsStore,
        IMagicLinkCommands magicLinkCommands, ISessionCommands sessionCommands,
        IWallOfShameQueries wallOfShameQueries, TimeProvider timeProvider,
        HttpContext httpContext, MagicLinkSignInRequest request)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        return await securityConfiguration.Value.Jwt.Match(
            none: () => Task.FromResult(Results.InternalServerError()),
            jwtSettings => settingsStore.Current.Match(
                none: () => Task.FromResult(Results.NotFound()),
                async settings =>
                {
                    var now = timeProvider.GetUtcNow();
                    var credential = MagicLinkCredential.TryParse(request.Token)
                        .Where(c => c.IsSignedBy(settings.ValidSecrets(now), jwtSettings.SigningKey));

                    var userId = await credential.Match(
                        none: () => Task.FromResult<Option<Guid>>(None),
                        some: c => magicLinkCommands.Consume(c.Nonce, now, httpContext.RequestAborted));

                    var user = await userId.Match(
                        none: () => Task.FromResult<ApplicationUser?>(null),
                        some: id => userManager.FindByIdAsync(id.ToString()));

                    return user is null
                        ? Results.Unauthorized()
                        : await StartSession(user, jwtSettings, sessionCommands, wallOfShameQueries, now, httpContext);
                }));
    }

    private static Option<Uri> FrontendUri(IConfiguration configuration) =>
        configuration.GetSection(FrontendConfiguration.SectionName).GetValue<Uri>(nameof(FrontendConfiguration.Url))
            is { } url
            ? Some(url)
            : None;

    private static async Task<IResult> StartSession(ApplicationUser user, JwtSettings jwtSettings,
        ISessionCommands sessionCommands, IWallOfShameQueries wallOfShameQueries, DateTimeOffset now,
        HttpContext httpContext)
    {
        var ban = await wallOfShameQueries.FindActiveBan(user.Id, now, httpContext.RequestAborted);

        return await ban.Match(
            none: () => StartPermittedSession(user, jwtSettings, sessionCommands, httpContext),
            some: active => Task.FromResult(Results.Json(
                new BannedResponse(active.Reason),
                statusCode: StatusCodes.Status403Forbidden)));
    }

    private static async Task<IResult> StartPermittedSession(ApplicationUser user, JwtSettings jwtSettings,
        ISessionCommands sessionCommands, HttpContext httpContext)
    {
        var humanUser = new HumanUser(user.DisplayName, user.Emails, None);
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

        return Results.Json(new SignInResponse(
            accessToken.ToJwtToken(user, sessionId, jwtSettings),
            accessToken.ExpiresAt,
            refreshToken.ToJwtToken(user, sessionId, jwtSettings)
        ));
    }
}
